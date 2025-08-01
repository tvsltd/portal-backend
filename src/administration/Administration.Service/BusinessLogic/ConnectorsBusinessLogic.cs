/********************************************************************************
 * Copyright (c) 2022 Contributors to the Eclipse Foundation
 *
 * See the NOTICE file(s) distributed with this work for additional
 * information regarding copyright ownership.
 *
 * This program and the accompanying materials are made available under the
 * terms of the Apache License, Version 2.0 which is available at
 * https://www.apache.org/licenses/LICENSE-2.0.
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * SPDX-License-Identifier: Apache-2.0
 ********************************************************************************/

using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.Administration.Service.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Administration.Service.Models;
using Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library;
using Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.Extensions;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Async;
using Org.Eclipse.TractusX.Portal.Backend.Framework.DateTimeProvider;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Identity;
using Org.Eclipse.TractusX.Portal.Backend.Framework.IO;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Linq;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Models;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Processes.Library.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Processes.Library.Extensions;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.SdFactory.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.SdFactory.Library.Models;
using System.Text.RegularExpressions;

namespace Org.Eclipse.TractusX.Portal.Backend.Administration.Service.BusinessLogic;

/// <summary>
/// Implementation of <see cref="IConnectorsBusinessLogic"/> making use of <see cref="IConnectorsRepository"/> to retrieve data.
/// </summary>
public class ConnectorsBusinessLogic(
    IPortalRepositories portalRepositories,
    IOptions<ConnectorsSettings> connectorOptions,
    IOptions<ClearinghouseSettings> clearinghouseOptions,
    ISdFactoryBusinessLogic sdFactoryBusinessLogic,
    IIdentityService identityService,
    IServiceAccountManagement serviceAccountManagement,
    IDateTimeProvider dateTimeProvider,
    ILogger<ConnectorsBusinessLogic> logger)
    : IConnectorsBusinessLogic
{
    private static readonly Regex BpnRegex = new(@"(\w|\d){16}", RegexOptions.None, TimeSpan.FromSeconds(1));
    private readonly IIdentityData _identityData = identityService.IdentityData;
    private readonly ConnectorsSettings _connectorSettings = connectorOptions.Value;
    private readonly ClearinghouseSettings _clearinghouseSettings = clearinghouseOptions.Value;

    /// <inheritdoc/>
    public Task<Pagination.Response<ConnectorData>> GetAllCompanyConnectorDatas(int page, int size) =>
        Pagination.CreateResponseAsync(
            page,
            size,
            _connectorSettings.MaxPageSize,
            portalRepositories.GetInstance<IConnectorsRepository>().GetAllCompanyConnectorsForCompanyId(_identityData.CompanyId));

    /// <inheritdoc/>
    public Task<Pagination.Response<ConnectorData>> GetAllProvidedConnectorsData(int page, int size) =>
        Pagination.CreateResponseAsync(
            page,
            size,
            _connectorSettings.MaxPageSize,
            portalRepositories.GetInstance<IConnectorsRepository>().GetAllProvidedConnectorsForCompanyId(_identityData.CompanyId));

    /// <inheritdoc/>
    public Task<Pagination.Response<ManagedConnectorData>> GetManagedConnectorForCompany(int page, int size) =>
        Pagination.CreateResponseAsync(
            page,
            size,
            _connectorSettings.MaxPageSize,
            portalRepositories.GetInstance<IConnectorsRepository>().GetManagedConnectorsForCompany(_identityData.CompanyId));

    public async Task<ConnectorData> GetCompanyConnectorData(Guid connectorId)
    {
        var companyId = _identityData.CompanyId;
        var result = await portalRepositories.GetInstance<IConnectorsRepository>().GetConnectorByIdForCompany(connectorId, companyId).ConfigureAwait(ConfigureAwaitOptions.None);
        if (result == default)
        {
            throw NotFoundException.Create(AdministrationConnectorErrors.CONNECTOR_NOT_FOUND, new ErrorParameter[] { new(nameof(connectorId), connectorId.ToString()) });
        }

        if (!result.IsProvidingOrHostCompany)
        {
            throw ForbiddenException.Create(AdministrationConnectorErrors.CONNECTOR_NOT_PROVIDER_COMPANY_NOR_HOST, new ErrorParameter[] { new(nameof(companyId), companyId.ToString()), new(nameof(connectorId), connectorId.ToString()) });
        }

        return result.ConnectorData;
    }

    /// <inheritdoc/>
    public Task<Guid> CreateConnectorAsync(ConnectorInputModel connectorInputModel, CancellationToken cancellationToken) =>
        CreateConnectorInternalAsync(connectorInputModel, cancellationToken);

    public Task<Guid> CreateManagedConnectorAsync(ManagedConnectorInputModel connectorInputModel, CancellationToken cancellationToken) =>
        CreateManagedConnectorInternalAsync(connectorInputModel, cancellationToken);

    private async Task<Guid> CreateConnectorInternalAsync(ConnectorInputModel connectorInputModel, CancellationToken cancellationToken)
    {
        var companyId = _identityData.CompanyId;
        var (name, connectorUrl, location, technicalUserId) = connectorInputModel;
        await CheckDuplicateConnector(name, connectorUrl).ConfigureAwait(ConfigureAwaitOptions.None);
        await CheckLocationExists(location);

        var result = await portalRepositories
            .GetInstance<ICompanyRepository>()
            .GetCompanyBpnAndSelfDescriptionDocumentByIdAsync(companyId)
            .ConfigureAwait(ConfigureAwaitOptions.None);

        if (string.IsNullOrEmpty(result.Bpn))
        {
            throw UnexpectedConditionException.Create(AdministrationConnectorErrors.CONNECTOR_UNEXPECTED_NO_BPN_ASSIGNED, new ErrorParameter[] { new(nameof(companyId), companyId.ToString()) });
        }

        await ValidateTechnicalUser(ConnectorTypeId.COMPANY_CONNECTOR, name, technicalUserId, companyId).ConfigureAwait(ConfigureAwaitOptions.None);

        var connectorRequestModel = new ConnectorRequestModel(name, connectorUrl, ConnectorTypeId.COMPANY_CONNECTOR, location, companyId, companyId, technicalUserId);
        return await CreateAndRegisterConnectorAsync(
            connectorRequestModel,
            result.Bpn,
            result.SelfDescriptionDocumentId,
            null,
            companyId,
            cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    private async Task<Guid> CreateManagedConnectorInternalAsync(ManagedConnectorInputModel connectorInputModel, CancellationToken cancellationToken)
    {
        var companyId = _identityData.CompanyId;
        var (name, connectorUrl, location, subscriptionId, technicalUserId) = connectorInputModel;
        await CheckDuplicateConnector(name, connectorUrl).ConfigureAwait(ConfigureAwaitOptions.None);
        await CheckLocationExists(location).ConfigureAwait(ConfigureAwaitOptions.None);

        var result = await portalRepositories.GetInstance<IOfferSubscriptionsRepository>()
            .CheckOfferSubscriptionWithOfferProvider(subscriptionId, companyId)
            .ConfigureAwait(ConfigureAwaitOptions.None);

        if (!result.Exists)
        {
            throw NotFoundException.Create(AdministrationConnectorErrors.CONNECTOR_NOT_OFFERSUBSCRIPTION_EXIST, new ErrorParameter[] { new(nameof(subscriptionId), subscriptionId.ToString()) });
        }

        if (!result.IsOfferProvider)
        {
            throw ForbiddenException.Create(AdministrationConnectorErrors.CONNECTOR_NOT_PROVIDER_COMPANY_OFFER);
        }

        if (result.OfferSubscriptionAlreadyLinked)
        {
            throw ConflictException.Create(AdministrationConnectorErrors.CONNECTOR_CONFLICT_OFFERSUBSCRIPTION_LINKED);
        }

        if (result.OfferSubscriptionStatus != OfferSubscriptionStatusId.ACTIVE &&
            result.OfferSubscriptionStatus != OfferSubscriptionStatusId.PENDING)
        {
            throw ConflictException.Create(AdministrationConnectorErrors.CONNECTOR_CONFLICT_STATUS_ACTIVE_OR_PENDING, new ErrorParameter[] { new("offerSubscriptionStatusIdActive", OfferSubscriptionStatusId.ACTIVE.ToString()), new("offerSubscriptionStatusIdPending", OfferSubscriptionStatusId.PENDING.ToString()) });
        }

        if (string.IsNullOrWhiteSpace(result.ProviderBpn))
        {
            throw ConflictException.Create(AdministrationConnectorErrors.CONNECTOR_CONFLICT_SET_BPN, new ErrorParameter[] { new("companyId", result.CompanyId.ToString()) });
        }

        await ValidateTechnicalUser(ConnectorTypeId.CONNECTOR_AS_A_SERVICE, name, technicalUserId, result.CompanyId).ConfigureAwait(ConfigureAwaitOptions.None);

        var connectorRequestModel = new ConnectorRequestModel(name, connectorUrl, ConnectorTypeId.CONNECTOR_AS_A_SERVICE, location, companyId, result.CompanyId, technicalUserId);
        return await CreateAndRegisterConnectorAsync(
            connectorRequestModel,
            result.ProviderBpn,
            result.SelfDescriptionDocumentId,
            subscriptionId,
            result.CompanyId,
            cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    private async Task CheckLocationExists(string location)
    {
        if (!await portalRepositories.GetInstance<ICountryRepository>()
                .CheckCountryExistsByAlpha2CodeAsync(location.ToUpper()).ConfigureAwait(ConfigureAwaitOptions.None))
        {
            throw ControllerArgumentException.Create(AdministrationConnectorErrors.CONNECTOR_ARGUMENT_LOCATION_NOT_EXIST, new ErrorParameter[] { new("location", location) });
        }
    }

    private async Task CheckDuplicateConnector(string name, string connectorUrl)
    {
        if (await portalRepositories.GetInstance<IConnectorsRepository>()
             .CheckConnectorExists(name, connectorUrl).ConfigureAwait(ConfigureAwaitOptions.None))
        {
            throw ConflictException.Create(AdministrationConnectorErrors.CONNECTOR_DUPLICATE, [new("name", name), new("connectorUrl", connectorUrl)]);
        }
    }

    private async Task ValidateTechnicalUser(ConnectorTypeId type, string name, Guid? technicalUserId, Guid companyId)
    {
        if (technicalUserId == null)
        {
            if (type != ConnectorTypeId.COMPANY_CONNECTOR)
                return;

            throw ControllerArgumentException.Create(AdministrationConnectorErrors.CONNECTOR_MISSING_TECH_USER, [new ErrorParameter("name", name)]);
        }

        var (activeUserExists, linkedToConnectorOrOffer) = await portalRepositories.GetInstance<ITechnicalUserRepository>()
                .CheckTechnicalUserDetailsAsync(technicalUserId!.Value, companyId).ConfigureAwait(ConfigureAwaitOptions.None);

        if (!activeUserExists)
        {
            throw ControllerArgumentException.Create(AdministrationConnectorErrors.CONNECTOR_ARGUMENT_TECH_USER_NOT_ACTIVE, [new ErrorParameter("technicalUserId", technicalUserId.Value.ToString()), new ErrorParameter("companyId", companyId.ToString())]);
        }

        if (linkedToConnectorOrOffer)
        {
            throw ControllerArgumentException.Create(AdministrationConnectorErrors.CONNECTOR_ARGUMENT_TECH_USER_IN_USE, [new ErrorParameter("technicalUserId", technicalUserId.Value.ToString())]);
        }
    }

    private async Task<Guid> CreateAndRegisterConnectorAsync(
        ConnectorRequestModel connectorInputModel,
        string businessPartnerNumber,
        Guid? selfDescriptionDocumentId,
        Guid? subscriptionId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var countrySpecificSettings = _clearinghouseSettings.GetCountrySpecificSettings(connectorInputModel.Location);
        if (selfDescriptionDocumentId is null && !countrySpecificSettings.ClearinghouseConnectDisabled)
        {
            throw ConflictException.Create(AdministrationConnectorErrors.CONNECTOR_CONFLICT_NO_DESCRIPTION, [new(nameof(companyId), companyId.ToString())]);
        }

        var (name, connectorUrl, type, location, provider, host, technicalUserId) = connectorInputModel;

        var connectorsRepository = portalRepositories.GetInstance<IConnectorsRepository>();
        var createdConnector = connectorsRepository.CreateConnector(
            name,
            location.ToUpper(),
            connectorUrl,
            connector =>
            {
                connector.ProviderId = provider;
                connector.HostId = host;
                connector.TypeId = type;
                connector.DateLastChanged = DateTimeOffset.UtcNow;
                connector.StatusId = countrySpecificSettings.ClearinghouseConnectDisabled ? ConnectorStatusId.ACTIVE : ConnectorStatusId.PENDING;
                connector.SdSkippedDate = countrySpecificSettings.ClearinghouseConnectDisabled ? dateTimeProvider.OffsetNow : null;
                if (technicalUserId != null)
                {
                    connector.TechnicalUserId = technicalUserId;
                }
            });

        if (subscriptionId != null)
        {
            connectorsRepository.CreateConnectorAssignedSubscriptions(createdConnector.Id, subscriptionId.Value);
        }

        if (!countrySpecificSettings.ClearinghouseConnectDisabled)
        {
            var selfDescriptionDocumentUrl = $"{_connectorSettings.SelfDescriptionDocumentUrl}/{selfDescriptionDocumentId}";
            await sdFactoryBusinessLogic
                .RegisterConnectorAsync(createdConnector.Id, selfDescriptionDocumentUrl, businessPartnerNumber, cancellationToken)
                .ConfigureAwait(ConfigureAwaitOptions.None);
        }

        await portalRepositories.SaveAsync().ConfigureAwait(ConfigureAwaitOptions.None);
        return createdConnector.Id;
    }

    /// <inheritdoc/>
    public async Task DeleteConnectorAsync(Guid connectorId, bool deleteServiceAccount)
    {
        var companyId = _identityData.CompanyId;
        var connectorsRepository = portalRepositories.GetInstance<IConnectorsRepository>();
        var processStepsToFilter = new[]
        {
            ProcessStepTypeId.CREATE_DIM_TECHNICAL_USER, ProcessStepTypeId.RETRIGGER_CREATE_DIM_TECHNICAL_USER,
            ProcessStepTypeId.AWAIT_CREATE_DIM_TECHNICAL_USER_RESPONSE
        };

        var result = await connectorsRepository.GetConnectorDeleteDataAsync(connectorId, companyId, processStepsToFilter).ConfigureAwait(ConfigureAwaitOptions.None) ?? throw NotFoundException.Create(AdministrationConnectorErrors.CONNECTOR_NOT_FOUND, new ErrorParameter[] { new(nameof(connectorId), connectorId.ToString()) });
        var countrySpecificSettings = _clearinghouseSettings.GetCountrySpecificSettings(result.Location);
        if (!result.IsProvidingOrHostCompany)
        {
            throw ForbiddenException.Create(AdministrationConnectorErrors.CONNECTOR_NOT_PROVIDER_COMPANY_NOR_HOST, [new(nameof(companyId), companyId.ToString()), new(nameof(connectorId), connectorId.ToString())]);
        }

        if (result is { ServiceAccountId: not null, UserStatusId: UserStatusId.ACTIVE or UserStatusId.PENDING } && deleteServiceAccount)
        {
            await serviceAccountManagement.DeleteServiceAccount(result.ServiceAccountId!.Value, result.DeleteServiceAccountData).ConfigureAwait(false);
        }

        switch (result.ConnectorStatus)
        {
            case ConnectorStatusId.PENDING when result.SelfDescriptionDocumentId == null:
                await DeleteConnectorWithoutDocuments(connectorId, result.ConnectorOfferSubscriptions, connectorsRepository);
                break;
            case ConnectorStatusId.PENDING:
                await DeleteConnectorWithDocuments(connectorId, result.SelfDescriptionDocumentId.Value, result.ConnectorOfferSubscriptions, connectorsRepository);
                break;
            // Connector should be able to deleted if the ClearinghouseConnectDisabled bit is disabled and no SD document was part of connector.
            case ConnectorStatusId.ACTIVE when countrySpecificSettings.ClearinghouseConnectDisabled:
                await DeleteConnectorWithoutDocuments(connectorId, result.ConnectorOfferSubscriptions, connectorsRepository);
                break;
            case ConnectorStatusId.ACTIVE when result.SelfDescriptionDocumentId == null && result.DocumentStatusId == null:
                await DeleteConnectorWithoutDocuments(connectorId, result.ConnectorOfferSubscriptions, connectorsRepository);
                break;
            case ConnectorStatusId.ACTIVE when result.SelfDescriptionDocumentId != null && result.DocumentStatusId != null:
                await DeleteConnector(connectorId, result.ConnectorOfferSubscriptions, result.SelfDescriptionDocumentId.Value, result.DocumentStatusId.Value, connectorsRepository);
                break;
            default:
                throw ConflictException.Create(AdministrationConnectorErrors.CONNECTOR_CONFLICT_DELETION_DECLINED);
        }
    }

    private async Task DeleteConnector(Guid connectorId, IEnumerable<ConnectorOfferSubscription> connectorOfferSubscriptions, Guid selfDescriptionDocumentId, DocumentStatusId documentStatus, IConnectorsRepository connectorsRepository)
    {
        portalRepositories.GetInstance<IDocumentRepository>().AttachAndModifyDocument(
            selfDescriptionDocumentId,
            a => { a.DocumentStatusId = documentStatus; },
            a => { a.DocumentStatusId = DocumentStatusId.INACTIVE; });
        RemoveConnectorAssignedOfferSubscriptions(connectorId, connectorOfferSubscriptions, connectorsRepository);
        await DeleteUpdateConnectorDetail(connectorId, connectorsRepository);
    }

    private async Task DeleteUpdateConnectorDetail(Guid connectorId, IConnectorsRepository connectorsRepository)
    {
        connectorsRepository.AttachAndModifyConnector(connectorId, null, con =>
        {
            con.TechnicalUserId = null;
            con.StatusId = ConnectorStatusId.INACTIVE;
            con.DateLastChanged = DateTimeOffset.UtcNow;
        });
        await portalRepositories.SaveAsync();
    }

    private async Task DeleteConnectorWithDocuments(Guid connectorId, Guid selfDescriptionDocumentId, IEnumerable<ConnectorOfferSubscription> connectorOfferSubscriptions, IConnectorsRepository connectorsRepository)
    {
        portalRepositories.GetInstance<IDocumentRepository>().RemoveDocument(selfDescriptionDocumentId);
        RemoveConnectorAssignedOfferSubscriptions(connectorId, connectorOfferSubscriptions, connectorsRepository);
        connectorsRepository.DeleteConnector(connectorId);
        await portalRepositories.SaveAsync();
    }

    private async Task DeleteConnectorWithoutDocuments(Guid connectorId, IEnumerable<ConnectorOfferSubscription> connectorOfferSubscriptions, IConnectorsRepository connectorsRepository)
    {
        RemoveConnectorAssignedOfferSubscriptions(connectorId, connectorOfferSubscriptions, connectorsRepository);
        connectorsRepository.DeleteConnector(connectorId);
        await portalRepositories.SaveAsync();
    }

    private static void RemoveConnectorAssignedOfferSubscriptions(Guid connectorId, IEnumerable<ConnectorOfferSubscription> connectorOfferSubscriptions, IConnectorsRepository connectorsRepository)
    {
        var activeConnectorOfferSubscription = connectorOfferSubscriptions.Where(cos => cos.OfferSubscriptionStatus == OfferSubscriptionStatusId.ACTIVE)
            .Select(cos => cos.AssignedOfferSubscriptionIds);
        if (activeConnectorOfferSubscription.Any())
        {
            throw ForbiddenException.Create(AdministrationConnectorErrors.CONNECTOR_DELETION_FAILED_OFFER_SUBSCRIPTION, new ErrorParameter[] { new(nameof(connectorId), connectorId.ToString()), new("activeConnectorOfferSubscription", string.Join(",", activeConnectorOfferSubscription)) });
        }

        var assignedOfferSubscriptions = connectorOfferSubscriptions.Select(cos => cos.AssignedOfferSubscriptionIds);
        if (assignedOfferSubscriptions.Any())
        {
            connectorsRepository.DeleteConnectorAssignedSubscriptions(connectorId, assignedOfferSubscriptions);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<ConnectorEndPointData> GetCompanyConnectorEndPointAsync(IEnumerable<string>? bpns)
    {
        bpns ??= Enumerable.Empty<string>();

        bpns.Where(bpn => !BpnRegex.IsMatch(bpn)).IfAny(invalid =>
            throw ControllerArgumentException.Create(AdministrationConnectorErrors.CONNECTOR_ARGUMENT_INCORRECT_BPN, new ErrorParameter[] { new("bpns", string.Join(", ", invalid)) }));

        return portalRepositories.GetInstance<IConnectorsRepository>()
            .GetConnectorEndPointDataAsync(bpns.Select(x => x.ToUpper()))
            .PreSortedGroupBy(data => data.BusinessPartnerNumber)
            .Select(group =>
                new ConnectorEndPointData(
                    group.Key,
                    group.Select(x => x.ConnectorEndpoint)));
    }

    /// <inheritdoc />
    public async Task ProcessClearinghouseSelfDescription(SelfDescriptionResponseData data, CancellationToken cancellationToken)
    {
        logger.LogInformation("Process SelfDescription called with the following data {@Data}", data.ToString().Replace(Environment.NewLine, string.Empty));

        var result = await portalRepositories.GetInstance<IConnectorsRepository>()
            .GetConnectorDataById(data.ExternalId)
            .ConfigureAwait(ConfigureAwaitOptions.None);

        if (result == default)
        {
            throw NotFoundException.Create(AdministrationConnectorErrors.CONNECTOR_NOT_EXIST, new ErrorParameter[] { new("externalId", data.ExternalId.ToString()) });
        }

        if (result.SelfDescriptionDocumentId != null)
        {
            throw ConflictException.Create(AdministrationConnectorErrors.CONNECTOR_CONFLICT_ALREADY_ASSIGNED, new ErrorParameter[] { new("externalId", data.ExternalId.ToString()) });
        }

        await sdFactoryBusinessLogic.ProcessFinishSelfDescriptionLpForConnector(data, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);
        await portalRepositories.SaveAsync().ConfigureAwait(ConfigureAwaitOptions.None);
    }

    /// <inheritdoc />
    public Task UpdateConnectorUrl(Guid connectorId, ConnectorUpdateRequest data, CancellationToken cancellationToken)
    {
        data.ConnectorUrl.EnsureValidHttpUrl(() => nameof(data.ConnectorUrl));
        return UpdateConnectorUrlInternal(connectorId, data, cancellationToken);
    }

    private async Task UpdateConnectorUrlInternal(Guid connectorId, ConnectorUpdateRequest data, CancellationToken cancellationToken)
    {
        var connectorsRepository = portalRepositories
            .GetInstance<IConnectorsRepository>();
        var documentRepository = portalRepositories
           .GetInstance<IDocumentRepository>();
        var connector = await connectorsRepository
            .GetConnectorUpdateInformation(connectorId, _identityData.CompanyId)
            .ConfigureAwait(ConfigureAwaitOptions.None);

        if (connector == null)
        {
            throw NotFoundException.Create(AdministrationConnectorErrors.CONNECTOR_NOT_FOUND, [new(nameof(connectorId), connectorId.ToString())]);
        }

        if (connector.ConnectorUrl == data.ConnectorUrl)
        {
            return;
        }

        if (!connector.IsProviderCompany)
        {
            throw ForbiddenException.Create(AdministrationConnectorErrors.CONNECTOR_NOT_PROVIDER_COMPANY, [new("companyId", _identityData.CompanyId.ToString()), new(nameof(connectorId), connectorId.ToString())]);
        }

        if (connector.Status == ConnectorStatusId.INACTIVE)
        {
            throw ConflictException.Create(AdministrationConnectorErrors.CONNECTOR_CONFLICT_INACTIVE_STATE, [new(nameof(connectorId), connectorId.ToString()), new("connectorStatusId", ConnectorStatusId.INACTIVE.ToString())]);
        }

        var bpn = connector.Type == ConnectorTypeId.CONNECTOR_AS_A_SERVICE
            ? connector.Bpn
            : await portalRepositories.GetInstance<IUserRepository>()
                .GetCompanyBpnForIamUserAsync(_identityData.IdentityId)
                .ConfigureAwait(ConfigureAwaitOptions.None);
        if (string.IsNullOrWhiteSpace(bpn))
        {
            throw ConflictException.Create(AdministrationConnectorErrors.CONNECTOR_CONFLICT_SET_BPN, [new("companyId", _identityData.CompanyId.ToString())]);
        }

        connectorsRepository.AttachAndModifyConnector(connectorId, null, con =>
        {
            con.ConnectorUrl = data.ConnectorUrl;
        });

        if (connector.SelfDescriptionDocumentId != null)
        {
            documentRepository.AttachAndModifyDocument(connector.SelfDescriptionDocumentId.Value, null, doc =>
            {
                doc.DocumentStatusId = DocumentStatusId.INACTIVE;
            });
        }

        if (connector.SelfDescriptionCompanyDocumentId is null)
        {
            throw ConflictException.Create(AdministrationConnectorErrors.CONNECTOR_CONFLICT_NO_DESCRIPTION, [new(nameof(connectorId), connectorId.ToString())]);
        }

        var selfDescriptionDocumentUrl = $"{_connectorSettings.SelfDescriptionDocumentUrl}/{connector.SelfDescriptionCompanyDocumentId}";
        await sdFactoryBusinessLogic
            .RegisterConnectorAsync(connectorId, selfDescriptionDocumentUrl, bpn, cancellationToken)
            .ConfigureAwait(ConfigureAwaitOptions.None);

        await portalRepositories.SaveAsync().ConfigureAwait(ConfigureAwaitOptions.None);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<OfferSubscriptionConnectorData> GetConnectorOfferSubscriptionData(bool? connectorIdSet) =>
        portalRepositories.GetInstance<IOfferSubscriptionsRepository>()
            .GetConnectorOfferSubscriptionData(connectorIdSet, _identityData.CompanyId);

    public Task<Pagination.Response<ConnectorMissingSdDocumentData>> GetConnectorsWithMissingSdDocument(int page, int size) =>
        Pagination.CreateResponseAsync(
            page,
            size,
            _connectorSettings.MaxPageSize,
            portalRepositories.GetInstance<IConnectorsRepository>().GetConnectorsWithMissingSdDocument());

    public async Task TriggerSelfDescriptionCreation()
    {
        var connectorRepository = portalRepositories.GetInstance<IConnectorsRepository>();
        var processStepRepository = portalRepositories.GetInstance<IPortalProcessStepRepository>();
        var connectorIds = connectorRepository.GetConnectorIdsWithMissingSelfDescription();
        await foreach (var connectorId in connectorIds)
        {
            var processId = processStepRepository.CreateProcess(ProcessTypeId.SELF_DESCRIPTION_CREATION).Id;
            processStepRepository.CreateProcessStep(ProcessStepTypeId.SELF_DESCRIPTION_CONNECTOR_CREATION, ProcessStepStatusId.TODO, processId);
            connectorRepository.AttachAndModifyConnector(connectorId, c => c.SdCreationProcessId = null, c => c.SdCreationProcessId = processId);
        }

        await portalRepositories.SaveAsync().ConfigureAwait(ConfigureAwaitOptions.None);
    }

    public Task RetriggerSelfDescriptionCreation(Guid processId) => RetriggerSelfDescriptionConnectorCreation(processId, ProcessStepTypeId.RETRIGGER_SELF_DESCRIPTION_CONNECTOR_CREATION);

    public Task RetriggerSelfDescriptionResponseCreation(Guid processId) => RetriggerSelfDescriptionConnectorCreation(processId, ProcessStepTypeId.RETRIGGER_AWAIT_SELF_DESCRIPTION_CONNECTOR_RESPONSE);

    private async Task RetriggerSelfDescriptionConnectorCreation(Guid processId, ProcessStepTypeId stepToTrigger)
    {
        const ProcessStepTypeId NextStep = ProcessStepTypeId.SELF_DESCRIPTION_CONNECTOR_CREATION;
        var (validProcessId, processData) = await portalRepositories.GetInstance<IPortalProcessStepRepository>().IsValidProcess(processId, ProcessTypeId.SELF_DESCRIPTION_CREATION, Enumerable.Repeat(stepToTrigger, 1)).ConfigureAwait(ConfigureAwaitOptions.None);
        if (!validProcessId)
        {
            throw new NotFoundException($"process {processId} does not exist");
        }

        var context = processData.CreateManualProcessData(stepToTrigger, portalRepositories, () => $"processId {processId}");

        context.ScheduleProcessSteps(Enumerable.Repeat(NextStep, 1));
        context.FinalizeProcessStep();
        await portalRepositories.SaveAsync().ConfigureAwait(ConfigureAwaitOptions.None);
    }
}
