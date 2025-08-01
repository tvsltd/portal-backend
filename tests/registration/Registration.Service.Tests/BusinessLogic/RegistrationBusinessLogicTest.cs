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

using AutoFixture;
using AutoFixture.AutoFakeItEasy;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.Bpdm.Library;
using Org.Eclipse.TractusX.Portal.Backend.Bpdm.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.Framework.DateTimeProvider;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Identity;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Models.Configuration;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Processes.Library.Concrete.Entities;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Processes.Library.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Tests.Shared;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Processes.ApplicationChecklist.Library;
using Org.Eclipse.TractusX.Portal.Backend.Processes.Mailing.Library;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library.Service;
using Org.Eclipse.TractusX.Portal.Backend.Registration.Common;
using Org.Eclipse.TractusX.Portal.Backend.Registration.Common.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Registration.Service.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Registration.Service.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Registration.Service.Model;
using Org.Eclipse.TractusX.Portal.Backend.Tests.Shared;
using Org.Eclipse.TractusX.Portal.Backend.Tests.Shared.Extensions;
using System.Collections.Immutable;
using Xunit;
using Address = Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities.Address;
using RegistrationData = Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models.RegistrationData;

namespace Org.Eclipse.TractusX.Portal.Backend.Registration.Service.Tests.BusinessLogic;

public class RegistrationBusinessLogicTest
{
    private readonly IFixture _fixture;
    private readonly IUserProvisioningService _userProvisioningService;
    private readonly IIdentityProviderProvisioningService _identityProviderProvisioningService;
    private readonly IInvitationRepository _invitationRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserRolesRepository _userRoleRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly ICountryRepository _countryRepository;
    private readonly IPortalRepositories _portalRepositories;
    private readonly ICompanyRolesRepository _companyRolesRepository;
    private readonly IMailingProcessCreation _mailingProcessCreation;
    private readonly IConsentRepository _consentRepository;
    private readonly IPortalProcessStepRepository _processStepRepository;
    private readonly IIdentityProviderRepository _identityProviderRepository;
    private readonly IApplicationChecklistCreationService _checklistService;
    private readonly IIdentityData _identity;
    private readonly Guid _existingApplicationId;
    private readonly string _displayName;
    private readonly string _alpha2code;
    private readonly string _region;
    private readonly string _vatId;
    private readonly TestException _error;
    private readonly IOptions<RegistrationSettings> _options;
    private readonly IStaticDataRepository _staticDataRepository;

    private readonly
        Func<UserCreationRoleDataIdpInfo, (Guid CompanyUserId, string UserName, string? Password, Exception? Error)>
        _processLine;

    private readonly IIdentityService _identityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RegistrationBusinessLogicTest()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        _fixture.ConfigureFixture();

        _portalRepositories = A.Fake<IPortalRepositories>();
        _userProvisioningService = A.Fake<IUserProvisioningService>();
        _identityProviderProvisioningService = A.Fake<IIdentityProviderProvisioningService>();
        _invitationRepository = A.Fake<IInvitationRepository>();
        _documentRepository = A.Fake<IDocumentRepository>();
        _userRepository = A.Fake<IUserRepository>();
        _userRoleRepository = A.Fake<IUserRolesRepository>();
        _companyRepository = A.Fake<ICompanyRepository>();
        _companyRolesRepository = A.Fake<ICompanyRolesRepository>();
        _applicationRepository = A.Fake<IApplicationRepository>();
        _countryRepository = A.Fake<ICountryRepository>();
        _consentRepository = A.Fake<IConsentRepository>();
        _mailingProcessCreation = A.Fake<IMailingProcessCreation>();

        _checklistService = A.Fake<IApplicationChecklistCreationService>();
        _staticDataRepository = A.Fake<IStaticDataRepository>();
        _processStepRepository = A.Fake<IPortalProcessStepRepository>();
        _identityProviderRepository = A.Fake<IIdentityProviderRepository>();
        _dateTimeProvider = A.Fake<IDateTimeProvider>();

        _identityService = A.Fake<IIdentityService>();
        _identity = A.Fake<IIdentityData>();
        A.CallTo(() => _identity.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => _identity.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => _identity.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(_identity);

        var options = Options.Create(new RegistrationSettings
        {
            BasePortalAddress = "just a test",
            KeycloakClientID = "CatenaX",
        });
        _fixture.Inject(options);
        _fixture.Inject(_mailingProcessCreation);
        _fixture.Inject(A.Fake<IBpnAccess>());
        _fixture.Inject(A.Fake<ILogger<RegistrationBusinessLogic>>());

        _options = _fixture.Create<IOptions<RegistrationSettings>>();

        _existingApplicationId = _fixture.Create<Guid>();
        _displayName = _fixture.Create<string>();
        _alpha2code = "XY";
        _region = "XX";
        _vatId = "DE123456789";
        _error = _fixture.Create<TestException>();

        _processLine =
            A.Fake<Func<UserCreationRoleDataIdpInfo, (Guid CompanyUserId, string UserName, string? Password, Exception?
                Error)>>();

        SetupRepositories();

        _fixture.Inject(_userProvisioningService);
        _fixture.Inject(_portalRepositories);
    }

    #region GetClientRolesComposite

    [Fact]
    public async Task GetClientRolesCompositeAsync_GetsAllRoles()
    {
        //Arrange
        var roles = _fixture.CreateMany<string>(3).ToImmutableArray();

        A.CallTo(() => _portalRepositories.GetInstance<IUserRolesRepository>().GetClientRolesCompositeAsync(A<string>._))
            .Returns(roles.ToAsyncEnumerable());

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        // Act
        var result = await sut.GetClientRolesCompositeAsync().ToListAsync();

        // Assert
        A.CallTo(() => _userRoleRepository.GetClientRolesCompositeAsync("CatenaX")).MustHaveHappenedOnceExactly();
        result.Should().HaveSameCount(roles)
            .And.ContainInOrder(roles);
    }

    #endregion

    #region GetCompanyBpdmDetailDataByBusinessPartnerNumber

    [Fact]
    public async Task GetCompanyBpdmDetailDataByBusinessPartnerNumber_WithValidBpn_ReturnsExpected()
    {
        //Arrange
        var bpnAccess = A.Fake<IBpnAccess>();
        var businessPartnerNumber = "THISBPNISVALID12";
        var token = _fixture.Create<string>();
        var country = "XY";

        var uniqueIdSeed = _fixture
            .CreateMany<(BpdmIdentifierId BpdmIdentifierId, UniqueIdentifierId UniqueIdentifierId, string Value)>(5)
            .ToImmutableArray();
        var name = _fixture.Create<string>();
        var shortName = _fixture.Create<string>();
        var region = _fixture.Create<string>();
        var city = _fixture.Create<string>();
        var streetName = _fixture.Create<string>();
        var streetNumber = _fixture.Create<string>();
        var zipCode = _fixture.Create<string>();

        var bpdmIdentifiers = uniqueIdSeed.Select(x => (TechnicalKey: x.BpdmIdentifierId.ToString(), x.Value));
        var validIdentifiers = uniqueIdSeed.Skip(2).Take(2).Select(x => (x.BpdmIdentifierId, x.UniqueIdentifierId));

        var bpdmAddress = _fixture.Build<BpdmLegalEntityAddress>()
            .With(x => x.BpnLegalEntity, name)
            .With(x => x.Bpna, businessPartnerNumber)
            .With(x => x.PhysicalPostalAddress, _fixture.Build<BpdmPhysicalPostalAddress>()
                .With(x => x.Country, _fixture.Build<BpdmCountry>().With(x => x.TechnicalKey, country).Create())
                .With(x => x.PostalCode, zipCode)
                .With(x => x.City, city)
                .With(x => x.District, region)
                .With(x => x.Street,
                    _fixture.Build<BpdmStreet>().With(x => x.Name, streetName).With(x => x.HouseNumber, streetNumber)
                        .Create())
                .Create())
            .Create();
        var legalEntity = _fixture.Build<BpdmLegalEntityDto>()
            .With(x => x.Bpn, businessPartnerNumber)
            .With(x => x.LegalName, name)
            .With(x => x.LegalShortName, shortName)
            .With(x => x.Identifiers, bpdmIdentifiers.Select(identifier => _fixture.Build<BpdmIdentifierDto>()
                .With(x => x.Type,
                    _fixture.Build<BpdmTechnicalKey>().With(x => x.TechnicalKey, identifier.TechnicalKey).Create())
                .With(x => x.Value, identifier.Value)
                .Create()))
            .With(x => x.LegalEntityAddress, bpdmAddress)
            .Create();
        A.CallTo(() => bpnAccess.FetchLegalEntityByBpn(businessPartnerNumber, token, A<CancellationToken>._))
            .Returns(legalEntity);
        A.CallTo(() => _staticDataRepository.GetCountryAssignedIdentifiers(
                A<IEnumerable<BpdmIdentifierId>>.That.Matches(ids =>
                    ids.SequenceEqual(uniqueIdSeed.Select(seed => seed.BpdmIdentifierId))), country))
            .Returns((true, validIdentifiers));

        var sut = new RegistrationBusinessLogic(
            _options,
            bpnAccess,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        // Act
        var result = await sut
            .GetCompanyBpdmDetailDataByBusinessPartnerNumber(businessPartnerNumber, token, CancellationToken.None);

        A.CallTo(() => bpnAccess.FetchLegalEntityByBpn(businessPartnerNumber, token, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _staticDataRepository.GetCountryAssignedIdentifiers(
                A<IEnumerable<BpdmIdentifierId>>.That.Matches(ids =>
                    ids.SequenceEqual(uniqueIdSeed.Select(seed => seed.BpdmIdentifierId))), country))
            .MustHaveHappenedOnceExactly();

        result.Should().NotBeNull();
        result.BusinessPartnerNumber.Should().Be(businessPartnerNumber);
        result.CountryAlpha2Code.Should().Be(country);

        var expectedUniqueIds = uniqueIdSeed.Skip(2).Take(2)
            .Select(x => new CompanyUniqueIdData(x.UniqueIdentifierId, x.Value));
        result.UniqueIds.Should().HaveSameCount(expectedUniqueIds);
        result.UniqueIds.Should().ContainInOrder(expectedUniqueIds);

        result.Name.Should().Be(name);
        result.ShortName.Should().Be(shortName);
        result.Region.Should().Be(region);
        result.City.Should().Be(city);
        result.StreetName.Should().Be(streetName);
        result.StreetNumber.Should().Be(streetNumber);
        result.ZipCode.Should().Be(zipCode);
    }

    [Fact]
    public async Task GetCompanyBpdmDetailDataByBusinessPartnerNumber_WithValidBpn_ThrowsArgumentException()
    {
        //Arrange
        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        // Act
        Task Act() => sut.GetCompanyBpdmDetailDataByBusinessPartnerNumber("NotLongEnough", "justatoken", CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_ARG_BPN_NOT_HAVING_SIXTEEN_LENGTH.ToString());
        ex.Parameters.First().Name.Should().Be("businessPartnerNumber");
    }

    #endregion

    #region GetAllApplicationsForUserWithStatus

    [Fact]
    public async Task GetAllApplicationsForUserWithStatus_WithValidUser_GetsAllRoles()
    {
        //Arrange
        var userCompanyId = _fixture.Create<Guid>();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(userCompanyId);
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        var resultList = new[]
        {
            new CompanyApplicationWithStatus(
                _fixture.Create<Guid>(),
                CompanyApplicationStatusId.VERIFY,
                CompanyApplicationTypeId.INTERNAL,
                [
                    new ApplicationChecklistData(ApplicationChecklistEntryTypeId.APPLICATION_ACTIVATION,
                        ApplicationChecklistEntryStatusId.DONE),
                    new ApplicationChecklistData(ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER,
                        ApplicationChecklistEntryStatusId.DONE),
                    new ApplicationChecklistData(ApplicationChecklistEntryTypeId.CLEARING_HOUSE,
                        ApplicationChecklistEntryStatusId.DONE),
                    new ApplicationChecklistData(ApplicationChecklistEntryTypeId.IDENTITY_WALLET,
                        ApplicationChecklistEntryStatusId.IN_PROGRESS),
                    new ApplicationChecklistData(ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION,
                        ApplicationChecklistEntryStatusId.FAILED),
                    new ApplicationChecklistData(ApplicationChecklistEntryTypeId.SELF_DESCRIPTION_LP,
                        ApplicationChecklistEntryStatusId.TO_DO)
                ])
        };
        A.CallTo(() => _userRepository.GetApplicationsWithStatusUntrackedAsync(userCompanyId))
            .Returns(resultList.ToAsyncEnumerable());

        // Act
        var result = await sut.GetAllApplicationsForUserWithStatus().ToListAsync();
        result.Should().ContainSingle();
        result.Single().ApplicationStatus.Should().Be(CompanyApplicationStatusId.VERIFY);
        result.Single().ApplicationChecklist.Should().NotBeNull().And.HaveCount(6).And.Satisfy(
            x => x.TypeId == ApplicationChecklistEntryTypeId.APPLICATION_ACTIVATION &&
                 x.StatusId == ApplicationChecklistEntryStatusId.DONE,
            x => x.TypeId == ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER &&
                 x.StatusId == ApplicationChecklistEntryStatusId.DONE,
            x => x.TypeId == ApplicationChecklistEntryTypeId.CLEARING_HOUSE &&
                 x.StatusId == ApplicationChecklistEntryStatusId.DONE,
            x => x.TypeId == ApplicationChecklistEntryTypeId.IDENTITY_WALLET &&
                 x.StatusId == ApplicationChecklistEntryStatusId.IN_PROGRESS,
            x => x.TypeId == ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION &&
                 x.StatusId == ApplicationChecklistEntryStatusId.FAILED,
            x => x.TypeId == ApplicationChecklistEntryTypeId.SELF_DESCRIPTION_LP &&
                 x.StatusId == ApplicationChecklistEntryStatusId.TO_DO
        );
    }

    #endregion

    #region GetCompanyWithAddress

    [Fact]
    public async Task GetCompanyWithAddressAsync_WithValidApplication_GetsData()
    {
        //Arrange
        var applicationId = _fixture.Create<Guid>();
        var data = _fixture.Build<CompanyApplicationDetailData>()
            .With(x => x.IsUserOfCompany, true)
            .Create();

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(A<Guid>._, A<Guid>._, A<Guid?>._))
            .Returns(data);

        // Act
        var result = await sut.GetCompanyDetailData(applicationId);

        // Assert
        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(applicationId, _identity.CompanyId, null))
            .MustHaveHappenedOnceExactly();
        result.Should().NotBeNull();
        result.UniqueIds.Should().HaveSameCount(data.UniqueIds);
    }

    [Fact]
    public async Task GetCompanyWithAddressAsync_WithInvalidApplication_ThrowsNotFoundException()
    {
        //Arrange
        var applicationId = _fixture.Create<Guid>();
        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(A<Guid>._, A<Guid>._, A<Guid?>._))
            .Returns<CompanyApplicationDetailData?>(null);

        // Act
        Task Act() => sut.GetCompanyDetailData(applicationId);

        // Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_COMPANY_APPLICATION_NOT_FOUND.ToString());
        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(applicationId, _identity.CompanyId, null))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetCompanyWithAddressAsync_WithInvalidUser_ThrowsForbiddenException()
    {
        //Arrange
        var applicationId = _fixture.Create<Guid>();
        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(A<Guid>._, A<Guid>._, A<Guid?>._))
            .Returns(_fixture.Build<CompanyApplicationDetailData>().With(x => x.IsUserOfCompany, false).Create());

        // Act
        Task Act() => sut.GetCompanyDetailData(applicationId);

        // Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_USER_COMPANY_NOT_ASSIGNED_APPLICATION_ID.ToString());
        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(applicationId, _identity.CompanyId, null))
            .MustHaveHappenedOnceExactly();
    }

    #endregion

    #region SetCompanyWithAddress

    [Theory]
    [InlineData(null, null, null, null, "", new UniqueIdentifierId[] { }, new string[] { }, RegistrationValidationErrors.NAME_NOT_EMPTY)]
    [InlineData("filled", null, null, null, "", new UniqueIdentifierId[] { }, new string[] { }, RegistrationValidationErrors.CITY_NOT_EMPTY)]
    [InlineData("filled", "filled", null, null, "", new UniqueIdentifierId[] { }, new string[] { }, RegistrationValidationErrors.STREET_NOT_EMPTY)]
    [InlineData("filled", "filled", "filled", "", "", new UniqueIdentifierId[] { }, new string[] { }, RegistrationValidationErrors.COUNTRY_CODE_MIN_LENGTH)]
    [InlineData("filled", "filled", "filled", "XX", "", new UniqueIdentifierId[] { }, new string[] { }, RegistrationValidationErrors.REGION_INVALID)]
    [InlineData("filled", "filled", "filled", "XX", "XX",
        new[] { UniqueIdentifierId.VAT_ID, UniqueIdentifierId.LEI_CODE }, new[] { "filled", "" },
        RegistrationValidationErrors.UNIQUE_IDS_NO_EMPTY_VALUES)]
    [InlineData("filled", "filled", "filled", "XX", "XX",
        new[] { UniqueIdentifierId.VAT_ID, UniqueIdentifierId.VAT_ID },
        new[] { "filled", "filled" }, RegistrationValidationErrors.UNIQUE_IDS_NO_DUPLICATE_VALUES)]
    public async Task SetCompanyWithAddressAsync_WithMissingData_ThrowsArgumentException(string? name, string? city,
        string? streetName, string? countryCode, string region, IEnumerable<UniqueIdentifierId> uniqueIdentifierIds,
        IEnumerable<string> values, RegistrationValidationErrors error)
    {
        //Arrange
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        var uniqueIdData = uniqueIdentifierIds.Zip(values, (id, value) => new CompanyUniqueIdData(id, value));
        var companyData = new CompanyDetailData(Guid.NewGuid(), name!, city!, streetName!, countryCode!, null, null,
            region, null, null, null, uniqueIdData);

        // Act
        Task Act() => sut.SetCompanyDetailDataAsync(Guid.NewGuid(), companyData);

        // Assert
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be(error.ToString());
    }

    [Fact]
    public async Task SetCompanyWithAddressAsync_WithInvalidApplicationId_ThrowsNotFoundException()
    {
        //Arrange
        var applicationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        var companyData = new CompanyDetailData(companyId, "name", "munich", "main street", "de", null, null, _region,
            null, null, null, Enumerable.Empty<CompanyUniqueIdData>());

        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(applicationId, A<Guid>._, companyId))
            .Returns<CompanyApplicationDetailData?>(null);

        // Act
        Task Act() => sut.SetCompanyDetailDataAsync(applicationId, companyData);

        // Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_COMPANY_APPLICATION_FOR_COMPANY_ID_NOT_FOUND.ToString());
    }

    [Fact]
    public async Task SetCompanyWithAddressAsync_WithoutCompanyUserId_ThrowsForbiddenException()
    {
        //Arrange
        var applicationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(companyId);
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var companyData = new CompanyDetailData(companyId, "name", "munich", "main street", "de", null, null, _region,
            null, null, null, Enumerable.Empty<CompanyUniqueIdData>());

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(applicationId, A<Guid>._, companyId))
            .Returns(_fixture.Build<CompanyApplicationDetailData>().With(x => x.IsUserOfCompany, false).Create());

        // Act
        Task Act() => sut.SetCompanyDetailDataAsync(applicationId, companyData);

        // Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_USER_APPLICATION_NOT_ASSIGN_WITH_COMP_APPLICATION.ToString());
    }

    [Fact]
    public async Task SetCompanyWithAddressAsync__WithInvalidBpn_ThrowsControllerArgumentException()
    {
        //Arrange
        var applicationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(companyId);
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var companyData = _fixture.Build<CompanyDetailData>()
            .With(x => x.BusinessPartnerNumber, "invalid")
            .With(x => x.CompanyId, companyId)
            .With(x => x.CountryAlpha2Code, _alpha2code)
            .With(x => x.Region, _region)
            .Create();

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        // Act
        Task Act() => sut.SetCompanyDetailDataAsync(applicationId, companyData);

        // Assert
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should()
            .Be(RegistrationValidationErrors.BPN_INVALID.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Berlin")]
    public async Task SetCompanyWithAddressAsync__WithInvalidRegion_ThrowsControllerArgumentException(string invalidRegion)
    {
        //Arrange
        var applicationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(companyId);
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var companyData = _fixture.Build<CompanyDetailData>()
            .With(x => x.BusinessPartnerNumber, "BPNL00000001TEST")
            .With(x => x.CompanyId, companyId)
            .With(x => x.CountryAlpha2Code, _alpha2code)
            .With(x => x.Region, invalidRegion)
            .Create();

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        // Act
        Task Act() => sut.SetCompanyDetailDataAsync(applicationId, companyData);

        // Assert
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should()
            .Be(RegistrationValidationErrors.REGION_INVALID.ToString());
    }

    [Fact]
    public async Task SetCompanyWithAddressAsync__WithExistingBpn_ModifiesCompany()
    {
        //Arrange
        var applicationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        Company? company = null;
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(companyId);
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var companyData = _fixture.Build<CompanyDetailData>()
            .With(x => x.BusinessPartnerNumber, "BPNL00000001TEST")
            .With(x => x.CompanyId, companyId)
            .With(x => x.CountryAlpha2Code, _alpha2code)
            .With(x => x.Region, _region)
            .With(x => x.UniqueIds, [new CompanyUniqueIdData(UniqueIdentifierId.VAT_ID, _vatId)])
            .Create();
        A.CallTo(() => _companyRepository.CheckBpnExists("BPNL00000001TEST")).Returns(true);

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
           _userProvisioningService,
           _identityProviderProvisioningService,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        var existingData = _fixture.Build<CompanyApplicationDetailData>()
            .With(x => x.IsUserOfCompany, true)
            .Create();

        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(applicationId, A<Guid>._, companyId))
            .Returns(existingData);

        A.CallTo(() => _companyRepository.AttachAndModifyCompany(A<Guid>._, A<Action<Company>>._, A<Action<Company>>._))
            .Invokes((Guid companyId, Action<Company>? initialize, Action<Company> modify) =>
            {
                company = new Company(companyId, null!, default, default);
                initialize?.Invoke(company);
                modify(company);
            });

        // Act
        await sut.SetCompanyDetailDataAsync(applicationId, companyData);

        // Assert
        A.CallTo(() => _companyRepository.AttachAndModifyCompany(A<Guid>._, A<Action<Company>>._, A<Action<Company>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();

        company.Should().NotBeNull();
        company.Should().Match<Company>(c =>
            c.Id == companyId &&
            c.Name == companyData.Name &&
            c.Shortname == companyData.ShortName &&
            c.BusinessPartnerNumber == companyData.BusinessPartnerNumber);
    }

    [Theory]
    // Worldwide
    [InlineData(UniqueIdentifierId.VAT_ID, "WW129273398", "WW")]
    [InlineData(UniqueIdentifierId.VIES, "WW129273398", "WW")]
    [InlineData(UniqueIdentifierId.COMMERCIAL_REG_NUMBER, "München HRB 175450", "WW")]
    [InlineData(UniqueIdentifierId.COMMERCIAL_REG_NUMBER, "F1103R_HRB98814", "WW")]
    [InlineData(UniqueIdentifierId.EORI, "WW12345678912345", "WW")]
    [InlineData(UniqueIdentifierId.LEI_CODE, "529900T8BM49AURSDO55", "WW")]

    // DE
    [InlineData(UniqueIdentifierId.VAT_ID, "DE129273398", "DE")]
    [InlineData(UniqueIdentifierId.COMMERCIAL_REG_NUMBER, "München HRB 175450", "DE")]
    [InlineData(UniqueIdentifierId.COMMERCIAL_REG_NUMBER, "F1103R_HRB98814", "DE")]

    // FR
    [InlineData(UniqueIdentifierId.COMMERCIAL_REG_NUMBER, "849281571", "FR")]

    // MX
    [InlineData(UniqueIdentifierId.VAT_ID, "MX-1234567890", "MX")]
    [InlineData(UniqueIdentifierId.VAT_ID, "MX1234567890", "MX")]
    [InlineData(UniqueIdentifierId.VAT_ID, "MX1234567890&", "MX")]

    // IN
    [InlineData(UniqueIdentifierId.VAT_ID, "IN123456789", "IN")]
    [InlineData(UniqueIdentifierId.VAT_ID, "IN-123456789", "IN")]
    public async Task SetCompanyWithAddressAsync__WithCompanyNameChange_ModifiesCompany(UniqueIdentifierId uniqueIdentifierId, string identifierValue, string countryCode)
    {
        //Arrange
        var applicationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        Company? company = null;
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(companyId);
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var companyData = _fixture.Build<CompanyDetailData>()
            .With(x => x.Name, "Test Company Updated Name")
            .With(x => x.BusinessPartnerNumber, "BPNL00000001TEST")
            .With(x => x.CompanyId, companyId)
            .With(x => x.CountryAlpha2Code, countryCode)
            .With(x => x.Region, _region)
            .With(x => x.UniqueIds, [new CompanyUniqueIdData(uniqueIdentifierId, identifierValue)])
            .Create();

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            _userProvisioningService,
            _identityProviderProvisioningService,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        var existingData = _fixture.Build<CompanyApplicationDetailData>()
            .With(x => x.IsUserOfCompany, true)
            .With(x => x.Name, "Test Company")
            .Create();

        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(applicationId, A<Guid>._, companyId))
            .Returns(existingData);

        A.CallTo(() => _companyRepository.AttachAndModifyCompany(A<Guid>._, A<Action<Company>>._, A<Action<Company>>._))
            .Invokes((Guid companyId, Action<Company>? initialize, Action<Company> modify) =>
            {
                company = new Company(companyId, null!, default, default);
                initialize?.Invoke(company);
                modify(company);
            });

        // Act
        await sut.SetCompanyDetailDataAsync(applicationId, companyData);

        // Assert
        A.CallTo(() => _companyRepository.AttachAndModifyCompany(A<Guid>._, A<Action<Company>>._, A<Action<Company>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _identityProviderProvisioningService.UpdateCompanyNameInSharedIdentityProvider(identityData.CompanyId, companyData.Name)).MustHaveHappenedOnceExactly();

        company.Should().NotBeNull();
        company.Should().Match<Company>(c =>
            c.Id == companyId &&
            c.Name == companyData.Name &&
            c.Shortname == companyData.ShortName &&
            c.BusinessPartnerNumber == companyData.BusinessPartnerNumber);
    }

    [Fact]
    public async Task SetCompanyWithAddressAsync__WithoutCompanyNameChange_ModifiesCompany()
    {
        //Arrange
        var applicationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        Company? company = null;
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(companyId);
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var companyData = _fixture.Build<CompanyDetailData>()
            .With(x => x.Name, "Test Company")
            .With(x => x.BusinessPartnerNumber, "BPNL00000001TEST")
            .With(x => x.CompanyId, companyId)
            .With(x => x.CountryAlpha2Code, _alpha2code)
            .With(x => x.Region, _region)
            .With(x => x.UniqueIds, [new CompanyUniqueIdData(UniqueIdentifierId.VAT_ID, _vatId)])
            .Create();

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            _userProvisioningService,
            _identityProviderProvisioningService,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        var existingData = _fixture.Build<CompanyApplicationDetailData>()
            .With(x => x.IsUserOfCompany, true)
            .With(x => x.Name, "Test Company")
            .Create();

        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(applicationId, A<Guid>._, companyId))
            .Returns(existingData);

        A.CallTo(() => _companyRepository.AttachAndModifyCompany(A<Guid>._, A<Action<Company>>._, A<Action<Company>>._))
            .Invokes((Guid companyId, Action<Company>? initialize, Action<Company> modify) =>
            {
                company = new Company(companyId, null!, default, default);
                initialize?.Invoke(company);
                modify(company);
            });

        // Act
        await sut.SetCompanyDetailDataAsync(applicationId, companyData);

        // Assert
        A.CallTo(() => _companyRepository.AttachAndModifyCompany(A<Guid>._, A<Action<Company>>._, A<Action<Company>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _identityProviderProvisioningService.UpdateCompanyNameInSharedIdentityProvider(identityData.CompanyId, companyData.Name)).MustNotHaveHappened();

        company.Should().NotBeNull();
        company.Should().Match<Company>(c =>
            c.Id == companyId &&
            c.Name == companyData.Name &&
            c.Shortname == companyData.ShortName &&
            c.BusinessPartnerNumber == companyData.BusinessPartnerNumber);
    }

    [Theory]
    [InlineData(null, "X")]
    [InlineData("", "XX")]
    [InlineData("BPNL00000003CRHK", "XXX")]
    [InlineData("BPNL00000003CRHK", "0")]
    [InlineData("BPNL00000003CRHK", "02")]
    [InlineData("BPNL00000003CRHK", "123")]
    [InlineData("BPNL00000003CRHK", "023")]
    [InlineData("BPNL00000003CRHK", "X23")]
    public async Task SetCompanyWithAddressAsync_ModifyCompany(string? bpn, string region)
    {
        //Arrange
        var applicationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(companyId);
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var companyData = _fixture.Build<CompanyDetailData>()
            .With(x => x.BusinessPartnerNumber, bpn)
            .With(x => x.CompanyId, companyId)
            .With(x => x.CountryAlpha2Code, _alpha2code)
            .With(x => x.Region, region)
            .With(x => x.UniqueIds, [new CompanyUniqueIdData(UniqueIdentifierId.VAT_ID, _vatId)])
            .Create();

        var existingData = _fixture.Build<CompanyApplicationDetailData>()
            .With(x => x.IsUserOfCompany, true)
            .Create();

        Company? company = null;

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            _userProvisioningService,
            _identityProviderProvisioningService,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(applicationId, A<Guid>._, companyId))
            .Returns(existingData);

        A.CallTo(() => _companyRepository.AttachAndModifyCompany(A<Guid>._, A<Action<Company>>._, A<Action<Company>>._))
            .Invokes((Guid companyId, Action<Company>? initialize, Action<Company> modify) =>
            {
                company = new Company(companyId, null!, default, default);
                initialize?.Invoke(company);
                modify(company);
            });

        // Act
        await sut.SetCompanyDetailDataAsync(applicationId, companyData);

        // Assert
        A.CallTo(() => _companyRepository.AttachAndModifyCompany(A<Guid>._, A<Action<Company>>._, A<Action<Company>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();

        var expectedBpn = string.IsNullOrEmpty(companyData.BusinessPartnerNumber)
            ? null
            : companyData.BusinessPartnerNumber;
        company.Should().NotBeNull();
        company.Should().Match<Company>(c =>
            c.Id == companyId &&
            c.Name == companyData.Name &&
            c.Shortname == companyData.ShortName &&
            c.BusinessPartnerNumber == expectedBpn);
    }

    [Fact]
    public async Task SetCompanyWithAddressAsync_WithoutInitialCompanyAddress_CreatesAddress()
    {
        //Arrange
        var applicationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(companyId);

        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var companyData = _fixture.Build<CompanyDetailData>()
            .With(x => x.BusinessPartnerNumber, default(string?))
            .With(x => x.CompanyId, companyId)
            .With(x => x.CountryAlpha2Code, _alpha2code)
            .With(x => x.Region, _region)
            .With(x => x.UniqueIds, [new CompanyUniqueIdData(UniqueIdentifierId.VAT_ID, _vatId)])
            .Create();

        var existingData = _fixture.Build<CompanyApplicationDetailData>()
            .With(x => x.CompanyId, companyId)
            .With(x => x.AddressId, default(Guid?))
            .With(x => x.City, default(string?))
            .With(x => x.CountryAlpha2Code, default(string?))
            .With(x => x.Region, default(string?))
            .With(x => x.Streetadditional, default(string?))
            .With(x => x.Streetname, default(string?))
            .With(x => x.Streetnumber, default(string?))
            .With(x => x.Zipcode, default(string?))
            .With(x => x.IsUserOfCompany, true)
            .Create();

        Company? company = null;
        Address? address = null;

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            _userProvisioningService,
            _identityProviderProvisioningService,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(applicationId, A<Guid>._, companyId))
            .Returns(existingData);

        A.CallTo(() => _companyRepository.AttachAndModifyCompany(A<Guid>._, A<Action<Company>>._, A<Action<Company>>._))
            .Invokes((Guid companyId, Action<Company>? initialize, Action<Company> modify) =>
            {
                company = new Company(companyId, null!, default, default);
                initialize?.Invoke(company);
                modify(company);
            });

        A.CallTo(() => _companyRepository.CreateAddress(A<string>._, A<string>._, A<string>._, A<string>._, A<Action<Address>>._))
            .ReturnsLazily((string city, string streetName, string region, string alpha2Code, Action<Address>? setParameters) =>
            {
                address = new Address(addressId, city, streetName, region, alpha2Code, default);
                setParameters?.Invoke(address);
                return address;
            });

        // Act
        await sut.SetCompanyDetailDataAsync(applicationId, companyData);

        // Assert
        A.CallTo(() => _companyRepository.CreateAddress(A<string>._, A<string>._, A<string>._, A<string>._, A<Action<Address>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _companyRepository.CreateAddress(companyData.City, companyData.StreetName, companyData.Region,
                companyData.CountryAlpha2Code, A<Action<Address>>._))
            .MustHaveHappened();
        A.CallTo(() => _companyRepository.AttachAndModifyCompany(A<Guid>._, A<Action<Company>>._, A<Action<Company>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _companyRepository.AttachAndModifyCompany(companyId, A<Action<Company>>._, A<Action<Company>>._))
            .MustHaveHappened();
        A.CallTo(() => _companyRepository.AttachAndModifyAddress(A<Guid>._, A<Action<Address>>._, A<Action<Address>>._))
            .MustNotHaveHappened();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();

        company.Should().NotBeNull();
        address.Should().NotBeNull();

        company!.Id.Should().Be(companyId);
        company.AddressId.Should().Be(addressId);

        address.Should().Match<Address>(a =>
            a.Id == addressId &&
            a.City == companyData.City &&
            a.CountryAlpha2Code == companyData.CountryAlpha2Code &&
            a.Region == companyData.Region &&
            a.Streetadditional == companyData.StreetAdditional &&
            a.Streetname == companyData.StreetName &&
            a.Streetnumber == companyData.StreetNumber &&
            a.Zipcode == companyData.ZipCode);
    }

    [Fact]
    public async Task SetCompanyWithAddressAsync_WithInitialCompanyAddress_ModifyAddress()
    {
        //Arrange
        var applicationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(companyId);

        var companyData = _fixture.Build<CompanyDetailData>()
            .With(x => x.BusinessPartnerNumber, default(string?))
            .With(x => x.CompanyId, companyId)
            .With(x => x.CountryAlpha2Code, _alpha2code)
            .With(x => x.Region, _region)
            .With(x => x.UniqueIds, [new CompanyUniqueIdData(UniqueIdentifierId.VAT_ID, _vatId)])
            .Create();

        var existingData = _fixture.Build<CompanyApplicationDetailData>()
            .With(x => x.CompanyId, companyId)
            .With(x => x.IsUserOfCompany, true)
            .Create();

        Company? company = null;
        Address? address = null;

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
           _userProvisioningService,
            _identityProviderProvisioningService,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(applicationId, A<Guid>._, companyId))
            .Returns(existingData);

        A.CallTo(() => _companyRepository.AttachAndModifyCompany(A<Guid>._, A<Action<Company>>._, A<Action<Company>>._))
            .Invokes((Guid companyId, Action<Company>? initialize, Action<Company> modify) =>
            {
                company = new Company(companyId, null!, default, default);
                initialize?.Invoke(company);
                modify(company);
            });

        A.CallTo(() =>
                _companyRepository.AttachAndModifyAddress(existingData.AddressId!.Value, A<Action<Address>>._,
                    A<Action<Address>>._))
            .Invokes((Guid addressId, Action<Address>? initialize, Action<Address> modify) =>
            {
                address = new Address(addressId, null!, null!, null!, null!, default);
                initialize?.Invoke(address);
                modify(address);
            });

        // Act
        await sut.SetCompanyDetailDataAsync(applicationId, companyData);

        // Assert
        A.CallTo(() => _companyRepository.CreateAddress(A<string>._, A<string>._, A<string>._, A<string>._, A<Action<Address>?>._))
            .MustNotHaveHappened();
        A.CallTo(
                () => _companyRepository.AttachAndModifyAddress(A<Guid>._, A<Action<Address>>._, A<Action<Address>>._!))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _companyRepository.AttachAndModifyAddress(existingData.AddressId!.Value, A<Action<Address>>._,
                    A<Action<Address>>._!))
            .MustHaveHappened();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();

        company.Should().NotBeNull();
        address.Should().NotBeNull();

        company!.Id.Should().Be(companyId);
        company.AddressId.Should().Be(existingData.AddressId!.Value);

        address.Should().Match<Address>(a =>
            a.Id == existingData.AddressId!.Value &&
            a.City == companyData.City &&
            a.CountryAlpha2Code == companyData.CountryAlpha2Code &&
            a.Region == companyData.Region &&
            a.Streetadditional == companyData.StreetAdditional &&
            a.Streetname == companyData.StreetName &&
            a.Streetnumber == companyData.StreetNumber &&
            a.Zipcode == companyData.ZipCode);
    }

    [Fact]
    public async Task SetCompanyWithAddressAsync_WithUniqueIdentifiers_CreateModifyDeleteExpected()
    {
        //Arrange
        var applicationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var now = DateTimeOffset.Now;
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(companyId);

        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var uniqueIdentifiers = _fixture.CreateMany<UniqueIdentifierId>(4);

        var firstIdData = _fixture.Build<CompanyUniqueIdData>()
            .With(x => x.UniqueIdentifierId, uniqueIdentifiers.First())
            .With(x => x.Value, "HRB123456")
            .Create(); // shall not modify
        var secondIdData = _fixture.Build<CompanyUniqueIdData>()
            .With(x => x.UniqueIdentifierId, uniqueIdentifiers.ElementAt(1))
            .With(x => x.Value, "DE124356789")
            .Create(); // shall modify
        var thirdIdData = _fixture.Build<CompanyUniqueIdData>()
            .With(x => x.UniqueIdentifierId, uniqueIdentifiers.ElementAt(2))
            .With(x => x.Value, "54930084UKLVMY22DS16")
            .Create(); // shall create new

        var companyData = _fixture.Build<CompanyDetailData>()
            .With(x => x.BusinessPartnerNumber, default(string?))
            .With(x => x.CompanyId, companyId)
            .With(x => x.CountryAlpha2Code, _alpha2code)
            .With(x => x.Region, _region)
            .With(x => x.UniqueIds, [firstIdData, secondIdData, thirdIdData])
            .Create();

        var existingData = _fixture.Build<CompanyApplicationDetailData>()
            .With(x => x.UniqueIds,
            [
                (firstIdData.UniqueIdentifierId, firstIdData.Value), // shall be left unmodified
                (secondIdData.UniqueIdentifierId, _fixture.Create<string>()), // shall be modified
                (uniqueIdentifiers.ElementAt(3), _fixture.Create<string>())   // shall be deleted
            ])
            .With(x => x.IsUserOfCompany, true)
            .Create();
        var application = _fixture.Build<CompanyApplication>()
            .With(x => x.Id, applicationId)
            .Create();

        IEnumerable<(UniqueIdentifierId UniqueIdentifierId, string Value)>? initialIdentifiers = null;
        IEnumerable<(UniqueIdentifierId UniqueIdentifierId, string Value)>? modifiedIdentifiers = null;

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            _userProvisioningService,
            _identityProviderProvisioningService,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        A.CallTo(() => _dateTimeProvider.OffsetNow).Returns(now);
        A.CallTo(() => _applicationRepository.GetCompanyApplicationDetailDataAsync(applicationId, A<Guid>._, companyId))
            .Returns(existingData);

        A.CallTo(() => _companyRepository.CreateUpdateDeleteIdentifiers(A<Guid>._,
                A<IEnumerable<(UniqueIdentifierId, string)>>._, A<IEnumerable<(UniqueIdentifierId, string)>>._))
            .Invokes((Guid _, IEnumerable<(UniqueIdentifierId UniqueIdentifierId, string Value)> initial,
                IEnumerable<(UniqueIdentifierId UniqueIdentifierId, string Value)> modified) =>
            {
                initialIdentifiers = initial;
                modifiedIdentifiers = modified;
            });
        A.CallTo(() =>
                _applicationRepository.AttachAndModifyCompanyApplication(applicationId,
                    A<Action<CompanyApplication>>._))
            .Invokes((Guid _, Action<CompanyApplication> setOptionalFields) =>
            {
                setOptionalFields.Invoke(application);
            });

        // Act
        await sut.SetCompanyDetailDataAsync(applicationId, companyData);

        // Assert
        A.CallTo(() => _companyRepository.CreateUpdateDeleteIdentifiers(companyId,
                A<IEnumerable<(UniqueIdentifierId, string)>>._, A<IEnumerable<(UniqueIdentifierId, string)>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _companyRepository.CreateUpdateDeleteIdentifiers(A<Guid>.That.Not.IsEqualTo(companyId),
                A<IEnumerable<(UniqueIdentifierId, string)>>._, A<IEnumerable<(UniqueIdentifierId, string)>>._))
            .MustNotHaveHappened();
        A.CallTo(() =>
                _applicationRepository.AttachAndModifyCompanyApplication(applicationId,
                    A<Action<CompanyApplication>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();

        initialIdentifiers.Should().NotBeNull();
        modifiedIdentifiers.Should().NotBeNull();
        initialIdentifiers.Should().ContainInOrder(existingData.UniqueIds);
        modifiedIdentifiers.Should().ContainInOrder((firstIdData.UniqueIdentifierId, firstIdData.Value),
            (secondIdData.UniqueIdentifierId, secondIdData.Value), (thirdIdData.UniqueIdentifierId, thirdIdData.Value));
        application.DateLastChanged.Should().Be(now);
    }

    [Fact]
    public async Task SetCompanyWithAddressAsync_WithInvalidCountryCode_Throws()
    {
        //Arrange
        var companyData = _fixture.Build<CompanyDetailData>()
            .With(x => x.BusinessPartnerNumber, default(string?))
            .With(x => x.CountryAlpha2Code, _alpha2code)
            .With(x => x.Region, _region)
            .With(x => x.UniqueIds, [new CompanyUniqueIdData(UniqueIdentifierId.VAT_ID, _vatId)])
            .Create();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        A.CallTo(() =>
                _countryRepository.GetCountryAssignedIdentifiers(A<string>._, A<IEnumerable<UniqueIdentifierId>>._))
            .Returns((false, null!));

        // Act
        Task Act() => sut.SetCompanyDetailDataAsync(Guid.NewGuid(), companyData);

        //Assert
        var result = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        result.Message.Should().Be(RegistrationValidationErrors.COUNTRY_CODE_NOT_VALID.ToString());
    }

    [Fact]
    public async Task SetCompanyWithAddressAsync_WithInvalidUniqueIdentifiers_Throws()
    {
        //Arrange
        var identifiers = _fixture.CreateMany<UniqueIdentifierId>(2);
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);

        var companyData = _fixture.Build<CompanyDetailData>()
            .With(x => x.BusinessPartnerNumber, default(string?))
            .With(x => x.CountryAlpha2Code, _alpha2code)
            .With(x => x.Region, _region)
            .With(x => x.UniqueIds,
                identifiers.Select(id =>
                    _fixture.Build<CompanyUniqueIdData>()
                    .With(x => x.UniqueIdentifierId, id)
                    .With(x => x.Value, _vatId)
                    .Create()))
            .Create();

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        A.CallTo(() =>
                _countryRepository.GetCountryAssignedIdentifiers(_alpha2code, A<IEnumerable<UniqueIdentifierId>>._))
            .Returns((true, new[] { identifiers.First() }));

        // Act
        Task Act() => sut.SetCompanyDetailDataAsync(Guid.NewGuid(), companyData);

        //Assert
        var result = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        result.Message.Should()
            .Be(RegistrationValidationErrors.UNIQUE_IDS_INVALID_FOR_COUNTRY.ToString());
    }

    #endregion

    #region SetOwnCompanyApplicationStatus

    [Fact]
    public async Task SetOwnCompanyApplicationStatusAsync_WithInvalidStatus_ThrowsControllerArgumentException()
    {
        //Arrange
        var applicationId = _fixture.Create<Guid>();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        // Act
        Task Act() => sut.SetOwnCompanyApplicationStatusAsync(applicationId, 0);

        // Assert
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_ARG_STATUS_NOT_NULL.ToString());
    }

    [Fact]
    public async Task SetOwnCompanyApplicationStatusAsync_WithInvalidApplication_ThrowsNotFoundException()
    {
        //Arrange
        var applicationId = _fixture.Create<Guid>();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserDataAsync(A<Guid>._, A<Guid>._))
            .Returns<(bool, CompanyApplicationStatusId)>(default);

        // Act
        Task Act() => sut.SetOwnCompanyApplicationStatusAsync(applicationId, CompanyApplicationStatusId.VERIFY);

        // Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_COMPANY_APPLICATION_NOT_FOUND.ToString());
    }

    [Fact]
    public async Task SetOwnCompanyApplicationStatusAsync_WithInvalidStatus_ThrowsArgumentException()
    {
        //Arrange
        var applicationId = _fixture.Create<Guid>();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        var existingStatus = CompanyApplicationStatusId.CREATED;
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserDataAsync(A<Guid>._, A<Guid>._))
            .Returns((true, existingStatus));
        var status = CompanyApplicationStatusId.VERIFY;

        // Act
        Task Act() => sut.SetOwnCompanyApplicationStatusAsync(applicationId, status);

        // Assert
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_ARG_INVALID_STATUS_REQUEST_APPLICATION_STATUS.ToString());
    }

    [Theory]
    [InlineData(CompanyApplicationStatusId.CREATED, CompanyApplicationStatusId.CREATED, false)]
    [InlineData(CompanyApplicationStatusId.CREATED, CompanyApplicationStatusId.ADD_COMPANY_DATA, true)]
    [InlineData(CompanyApplicationStatusId.ADD_COMPANY_DATA, CompanyApplicationStatusId.ADD_COMPANY_DATA, false)]
    [InlineData(CompanyApplicationStatusId.ADD_COMPANY_DATA, CompanyApplicationStatusId.INVITE_USER, true)]
    [InlineData(CompanyApplicationStatusId.INVITE_USER, CompanyApplicationStatusId.INVITE_USER, false)]
    [InlineData(CompanyApplicationStatusId.INVITE_USER, CompanyApplicationStatusId.SELECT_COMPANY_ROLE, true)]
    [InlineData(CompanyApplicationStatusId.SELECT_COMPANY_ROLE, CompanyApplicationStatusId.SELECT_COMPANY_ROLE, false)]
    [InlineData(CompanyApplicationStatusId.SELECT_COMPANY_ROLE, CompanyApplicationStatusId.UPLOAD_DOCUMENTS, true)]
    [InlineData(CompanyApplicationStatusId.UPLOAD_DOCUMENTS, CompanyApplicationStatusId.UPLOAD_DOCUMENTS, false)]
    [InlineData(CompanyApplicationStatusId.UPLOAD_DOCUMENTS, CompanyApplicationStatusId.VERIFY, true)]
    [InlineData(CompanyApplicationStatusId.VERIFY, CompanyApplicationStatusId.VERIFY, false)]
    [InlineData(CompanyApplicationStatusId.VERIFY, CompanyApplicationStatusId.SUBMITTED, true)]
    public async Task SetOwnCompanyApplicationStatusAsync_WithValidData_SavesChanges(CompanyApplicationStatusId currentStatus, CompanyApplicationStatusId expectedStatus, bool shouldUpdate)
    {
        //Arrange
        var now = DateTimeOffset.Now;
        var applicationId = _fixture.Create<Guid>();
        var application = _fixture.Build<CompanyApplication>()
            .With(x => x.Id, applicationId)
            .Create();
        A.CallTo(() => _dateTimeProvider.OffsetNow).Returns(now);
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(applicationId, A<Action<CompanyApplication>>._))
            .Invokes((Guid _, Action<CompanyApplication> setOptionalFields) =>
            {
                setOptionalFields.Invoke(application);
            });
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserDataAsync(A<Guid>._, A<Guid>._))
            .Returns((true, currentStatus));

        // Act
        await sut.SetOwnCompanyApplicationStatusAsync(applicationId, expectedStatus);

        // Assert
        if (shouldUpdate)
        {
            A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(applicationId, A<Action<CompanyApplication>>._)).MustHaveHappenedOnceExactly();
            application.DateLastChanged.Should().Be(now);
            application.ApplicationStatusId.Should().Be(expectedStatus);
        }
        else
        {
            A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(applicationId, A<Action<CompanyApplication>>._)).MustNotHaveHappened();
        }
    }

    #endregion

    #region GetCompanyRoles

    [Fact]
    public async Task GetCompanyRolesAsync_()
    {
        //Arrange
        var companyRolesRepository = A.Fake<ICompanyRolesRepository>();
        A.CallTo(() => companyRolesRepository.GetCompanyRolesAsync(A<string?>._))
            .Returns(_fixture.CreateMany<CompanyRolesDetails>(2).ToAsyncEnumerable());
        A.CallTo(() => _portalRepositories.GetInstance<ICompanyRolesRepository>())
            .Returns(companyRolesRepository);
        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        // Act
        var result = await sut.GetCompanyRoles().ToListAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().HaveCount(2);
    }

    #endregion

    #region GetInvitedUser

    [Fact]
    public async Task Get_WhenThereAreInvitedUser_ShouldReturnInvitedUserWithRoles()
    {
        //Arrange
        var sut = _fixture.Create<RegistrationBusinessLogic>();

        //Act
        var result = sut.GetInvitedUsersAsync(_existingApplicationId);
        await foreach (var item in result)
        {
            //Assert
            A.CallTo(() => _invitationRepository.GetInvitedUserDetailsUntrackedAsync(_existingApplicationId)).MustHaveHappened(1, Times.OrMore);
            Assert.NotNull(item);
            Assert.IsType<InvitedUser>(item);
        }
    }

    [Fact]
    public async Task GetInvitedUsersDetail_ThrowException_WhenIdIsNull()
    {
        //Arrange
        var sut = _fixture.Create<RegistrationBusinessLogic>();

        //Act
        async Task Act() => await sut.GetInvitedUsersAsync(Guid.Empty).ToListAsync();

        // Assert
        await Assert.ThrowsAsync<Exception>(Act);
    }

    #endregion

    #region UploadDocument

    [Fact]
    public async Task UploadDocumentAsync_WithValidData_CreatesDocument()
    {
        // Arrange
        var now = DateTimeOffset.Now;
        var documentId = Guid.NewGuid();
        var file = FormFileHelper.GetFormFile("this is just a test", "superFile.pdf", "application/pdf");
        var documents = new List<Document>();
        var application = _fixture.Build<CompanyApplication>()
            .With(x => x.Id, _existingApplicationId)
            .Create();
        var settings = new RegistrationSettings
        {
            DocumentTypeIds = [
                DocumentTypeId.CX_FRAME_CONTRACT,
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        A.CallTo(() => _dateTimeProvider.OffsetNow).Returns(now);
        A.CallTo(() => _documentRepository.CreateDocument(A<string>._, A<byte[]>._, A<byte[]>._, A<MediaTypeId>._, A<DocumentTypeId>._, A<long>._, A<Action<Document>?>._))
            .Invokes((string documentName, byte[] documentContent, byte[] hash, MediaTypeId mediaTypeId, DocumentTypeId documentTypeId, long documentSize, Action<Document>? action) =>
            {
                var document = new Document(documentId, documentContent, hash, documentName, mediaTypeId, DateTimeOffset.UtcNow, DocumentStatusId.PENDING, documentTypeId, documentSize);
                action?.Invoke(document);
                documents.Add(document);
            });
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(_existingApplicationId, A<Action<CompanyApplication>>._))
            .Invokes((Guid _, Action<CompanyApplication> setOptionalFields) =>
            {
                setOptionalFields.Invoke(application);
            });
        var sut = new RegistrationBusinessLogic(
            Options.Create(settings),
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        // Act
        await sut.UploadDocumentAsync(_existingApplicationId, file, DocumentTypeId.CX_FRAME_CONTRACT, CancellationToken.None);

        // Assert
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(_existingApplicationId, A<Action<CompanyApplication>>._)).MustHaveHappenedOnceExactly();
        documents.Should().HaveCount(1);
        application.DateLastChanged.Should().Be(now);
    }

    [Fact]
    public async Task UploadDocumentAsync_WithJsonDocument_ThrowsException()
    {
        // Arrange
        var file = FormFileHelper.GetFormFile("this is just a test", "superFile.json", "application/json");
        var sut = _fixture.Create<RegistrationBusinessLogic>();

        // Act
        Task Act() => sut.UploadDocumentAsync(_existingApplicationId, file, DocumentTypeId.ADDITIONAL_DETAILS, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<UnsupportedMediaTypeException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_UNSUPPORTED_MEDIA_ONLY_PDF_ALLOWED.ToString());
    }

    [Fact]
    public async Task UploadDocumentAsync_WithEmptyTitle_ThrowsException()
    {
        // Arrange
        var file = FormFileHelper.GetFormFile("this is just a test", string.Empty, "application/pdf");
        var sut = _fixture.Create<RegistrationBusinessLogic>();

        // Act
        Task Act() => sut.UploadDocumentAsync(_existingApplicationId, file, DocumentTypeId.ADDITIONAL_DETAILS, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_ARG_FILE_NAME_NOT_NULL.ToString());
    }

    [Fact]
    public async Task UploadDocumentAsync_WithNotExistingApplicationId_ThrowsException()
    {
        // Arrange
        var file = FormFileHelper.GetFormFile("this is just a test", "superFile.pdf", "application/pdf");
        var settings = new RegistrationSettings
        {
            DocumentTypeIds = [
                DocumentTypeId.CX_FRAME_CONTRACT,
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        var sut = new RegistrationBusinessLogic(
            Options.Create(settings),
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);
        var notExistingId = Guid.NewGuid();

        // Act
        Task Act() => sut.UploadDocumentAsync(notExistingId, file, DocumentTypeId.CX_FRAME_CONTRACT, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_USER_COMPANY_NOT_ASSIGNED_APPLICATION_ID.ToString());
    }

    [Fact]
    public async Task UploadDocumentAsync_WithNotExistingIamUser_ThrowsException()
    {
        // Arrange
        var file = FormFileHelper.GetFormFile("this is just a test", "superFile.pdf", "application/pdf");
        var settings = new RegistrationSettings
        {
            DocumentTypeIds = [
                DocumentTypeId.CX_FRAME_CONTRACT,
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        A.CallTo(() => _portalRepositories.GetInstance<IApplicationRepository>().IsValidApplicationForCompany(A<Guid>._, A<Guid>._))
            .Returns(false);

        var sut = new RegistrationBusinessLogic(
            Options.Create(settings),
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        // Act
        Task Act() => sut.UploadDocumentAsync(_existingApplicationId, file, DocumentTypeId.CX_FRAME_CONTRACT, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_USER_COMPANY_NOT_ASSIGNED_APPLICATION_ID.ToString());
        A.CallTo(() => _portalRepositories.GetInstance<IApplicationRepository>().IsValidApplicationForCompany(_existingApplicationId, _identity.CompanyId))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task UploadDocumentAsync_WithInvalidDocumentTypeId_ThrowsException()
    {
        // Arrange
        var file = FormFileHelper.GetFormFile("this is just a test", "superFile.pdf", "application/pdf");
        var settings = new RegistrationSettings
        {
            DocumentTypeIds = [
                DocumentTypeId.CX_FRAME_CONTRACT,
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        var sut = new RegistrationBusinessLogic(
            Options.Create(settings),
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        // Act
        Task Act() => sut.UploadDocumentAsync(_existingApplicationId, file, DocumentTypeId.ADDITIONAL_DETAILS, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_ARG_CHECK_DOCUMENT_TYPE.ToString());
    }

    #endregion

    #region InviteNewUser

    [Fact]
    public async Task TestInviteNewUserAsyncSuccess()
    {
        SetupFakesForInvitation();

        var now = DateTimeOffset.Now;
        var userCreationInfo = _fixture.Create<UserCreationInfoWithMessage>();
        var application = _fixture.Build<CompanyApplication>()
            .With(x => x.Id, _existingApplicationId)
            .Create();
        A.CallTo(() => _dateTimeProvider.OffsetNow).Returns(now);
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(_existingApplicationId, A<Action<CompanyApplication>>._))
            .Invokes((Guid _, Action<CompanyApplication> setOptionalFields) =>
            {
                setOptionalFields.Invoke(application);
            });

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            _userProvisioningService,
            _identityProviderProvisioningService,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        await sut.InviteNewUserAsync(_existingApplicationId, userCreationInfo);

        A.CallTo(() => _userProvisioningService.CreateOwnCompanyIdpUsersAsync(A<CompanyNameIdpAliasData>._, A<IAsyncEnumerable<UserCreationRoleDataIdpInfo>>._, A<Action<UserCreationCallbackData>>._, A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() => _applicationRepository.CreateInvitation(A<Guid>.That.IsEqualTo(_existingApplicationId), A<Guid>._)).MustHaveHappened();
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(_existingApplicationId, A<Action<CompanyApplication>>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappened();
        A.CallTo(() => _mailingProcessCreation.CreateMailProcess(A<string>.That.IsEqualTo(userCreationInfo.eMail), A<string>._, A<IReadOnlyDictionary<string, string>>.That.Matches(x => x["companyName"] == _displayName)))
            .MustHaveHappened();
        application.DateLastChanged.Should().Be(now);
    }

    [Fact]
    public async Task TestInviteNewUserEmptyEmailThrows()
    {
        SetupFakesForInvitation();

        var userCreationInfo = _fixture.Build<UserCreationInfoWithMessage>()
            .WithNamePattern(x => x.firstName)
            .WithNamePattern(x => x.lastName)
            .With(x => x.eMail, "")
            .Create();

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            _userProvisioningService,
            _identityProviderProvisioningService,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        Task Act() => sut.InviteNewUserAsync(_existingApplicationId, userCreationInfo);

        var error = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        error.Message.Should().Be(RegistrationErrors.REGISTRATION_ARGUMENT_EMAIL_MUST_NOT_EMPTY.ToString());

        A.CallTo(() => _portalRepositories.SaveAsync()).MustNotHaveHappened();
        A.CallTo(() => _mailingProcessCreation.CreateMailProcess(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task TestInviteNewUserUserAlreadyExistsThrows()
    {
        SetupFakesForInvitation();

        A.CallTo(() => _userRepository.IsOwnCompanyUserWithEmailExisting(A<string>._, A<Guid>._)).Returns(true);

        var userCreationInfo = _fixture.Create<UserCreationInfoWithMessage>();

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            _userProvisioningService,
            _identityProviderProvisioningService,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        Task Act() => sut.InviteNewUserAsync(_existingApplicationId, userCreationInfo);

        var error = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        error.Message.Should().Be(RegistrationErrors.REGISTRATION_ARGUMENT_EMAIL_ALREADY_EXIST.ToString());

        A.CallTo(() => _userRepository.IsOwnCompanyUserWithEmailExisting(userCreationInfo.eMail, _identity.CompanyId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustNotHaveHappened();
        A.CallTo(() => _mailingProcessCreation.CreateMailProcess(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task TestInviteNewUserAsyncCreationErrorThrows()
    {
        SetupFakesForInvitation();

        A.CallTo(() => _processLine(A<UserCreationRoleDataIdpInfo>._)).ReturnsLazily(
            (UserCreationRoleDataIdpInfo creationInfo) => _fixture.Build<(Guid CompanyUserId, string UserName, string? Password, Exception? Error)>()
                .With(x => x.UserName, creationInfo.UserName)
                .With(x => x.Error, _error)
                .Create());

        var userCreationInfo = _fixture.Create<UserCreationInfoWithMessage>();

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            _userProvisioningService,
            _identityProviderProvisioningService,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        Task Act() => sut.InviteNewUserAsync(_existingApplicationId, userCreationInfo);

        var error = await Assert.ThrowsAsync<TestException>(Act);
        error.Message.Should().Be(_error.Message);

        A.CallTo(() => _userProvisioningService.CreateOwnCompanyIdpUsersAsync(A<CompanyNameIdpAliasData>._, A<IAsyncEnumerable<UserCreationRoleDataIdpInfo>>._, A<Action<UserCreationCallbackData>>._, A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() => _applicationRepository.CreateInvitation(A<Guid>.That.IsEqualTo(_existingApplicationId), A<Guid>._)).MustNotHaveHappened();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustNotHaveHappened();
        A.CallTo(() => _mailingProcessCreation.CreateMailProcess(A<string>.That.IsEqualTo(userCreationInfo.eMail), A<string>._, A<IReadOnlyDictionary<string, string>>._))
            .MustNotHaveHappened();
    }

    #endregion

    #region GetUploadedDocuments

    [Fact]
    public async Task GetUploadedDocumentsAsync_ReturnsExpectedOutput()
    {
        // Arrange
        var applicationId = _fixture.Create<Guid>();
        var identity = _fixture.Create<IIdentityData>();
        A.CallTo(() => _identityService.IdentityData).Returns(identity);
        var uploadDocuments = _fixture.CreateMany<UploadDocuments>(3);

        A.CallTo(() => _documentRepository.GetUploadedDocumentsAsync(applicationId, DocumentTypeId.APP_CONTRACT, identity.IdentityId))
            .Returns((true, uploadDocuments));

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);
        // Act
        var result = await sut.GetUploadedDocumentsAsync(applicationId, DocumentTypeId.APP_CONTRACT);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveSameCount(uploadDocuments);
        result.Should().ContainInOrder(uploadDocuments);
    }

    [Fact]
    public async Task GetUploadedDocumentsAsync_InvalidApplication_ThrowsNotFound()
    {
        // Arrange
        var applicationId = _fixture.Create<Guid>();
        var identity = _fixture.Create<IIdentityData>();
        A.CallTo(() => _identityService.IdentityData).Returns(identity);

        A.CallTo(() => _documentRepository.GetUploadedDocumentsAsync(applicationId, DocumentTypeId.APP_CONTRACT, identity.IdentityId))
            .Returns<(bool, IEnumerable<UploadDocuments>)>(default);

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        Task Act() => sut.GetUploadedDocumentsAsync(applicationId, DocumentTypeId.APP_CONTRACT);

        // Act
        var error = await Assert.ThrowsAsync<NotFoundException>(Act);

        // Assert
        error.Message.Should().Be(RegistrationErrors.REGISTRATION_COMPANY_APPLICATION_NOT_FOUND.ToString());
    }

    [Fact]
    public async Task GetUploadedDocumentsAsync_InvalidUser_ThrowsForbidden()
    {
        // Arrange
        var applicationId = _fixture.Create<Guid>();
        var identity = _fixture.Create<IIdentityData>();
        A.CallTo(() => _identityService.IdentityData).Returns(identity);

        A.CallTo(() => _documentRepository.GetUploadedDocumentsAsync(applicationId, DocumentTypeId.APP_CONTRACT, identity.IdentityId))
            .Returns((false, Enumerable.Empty<UploadDocuments>()));

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        Task Act() => sut.GetUploadedDocumentsAsync(applicationId, DocumentTypeId.APP_CONTRACT);

        // Act
        var error = await Assert.ThrowsAsync<ForbiddenException>(Act);

        // Assert
        error.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_USER_NOT_ASSOCIATED_APPLICATION.ToString());
    }

    #endregion

    #region SubmitRoleConsents

    [Fact]
    public async Task SubmitRoleConsentsAsync_WithNotExistingApplication_ThrowsNotFoundException()
    {
        // Arrange
        var notExistingId = _fixture.Create<Guid>();
        A.CallTo(() => _companyRolesRepository.GetCompanyRoleAgreementConsentDataAsync(notExistingId))
            .Returns<CompanyRoleAgreementConsentData?>(null);
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRoleConsentAsync(notExistingId, _fixture.Create<CompanyRoleAgreementConsents>());

        // Arrange
        var ex = await Assert.ThrowsAsync<NotFoundException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_APPLICATION_NOT_EXIST.ToString());
    }

    [Fact]
    public async Task SubmitRoleConsentsAsync_WithWrongCompanyUser_ThrowsForbiddenException()
    {
        // Arrange
        var applicationId = _fixture.Create<Guid>();
        var applicationStatusId = _fixture.Create<CompanyApplicationStatusId>();
        var data = new CompanyRoleAgreementConsentData(Guid.NewGuid(), applicationStatusId, _fixture.CreateMany<CompanyRoleId>(2), _fixture.CreateMany<ConsentData>(5));
        A.CallTo(() => _companyRolesRepository.GetCompanyRoleAgreementConsentDataAsync(applicationId))
            .Returns(data);
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRoleConsentAsync(applicationId, _fixture.Create<CompanyRoleAgreementConsents>());

        // Arrange
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_USER_APPLICATION_NOT_ASSIGN_WITH_COMP_APPLICATION.ToString());
    }

    [Fact]
    public async Task SubmitRoleConsentsAsync_WithInvalidRoles_ThrowsControllerArgumentException()
    {
        // Arrange
        var applicationId = _fixture.Create<Guid>();
        var applicationStatusId = _fixture.Create<CompanyApplicationStatusId>();
        var data = new CompanyRoleAgreementConsentData(_identity.CompanyId, applicationStatusId, _fixture.CreateMany<CompanyRoleId>(2), _fixture.CreateMany<ConsentData>(5));
        var roleIds = new CompanyRoleId[]
        {
            CompanyRoleId.APP_PROVIDER,
        };
        var companyRoleAssignedAgreements = new[]
        {
            (CompanyRoleId.APP_PROVIDER, _fixture.CreateMany<AgreementStatusData>(5)),
        };
        A.CallTo(() => _companyRolesRepository.GetCompanyRoleAgreementConsentDataAsync(applicationId))
            .Returns(data);
        A.CallTo(() => _companyRolesRepository.GetAgreementAssignedCompanyRolesUntrackedAsync(roleIds))
            .Returns(companyRoleAssignedAgreements.ToAsyncEnumerable());
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRoleConsentAsync(applicationId, _fixture.Create<CompanyRoleAgreementConsents>());

        // Arrange
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_ARG_INVALID_COMPANY_ROLES.ToString());
    }

    [Fact]
    public async Task SubmitRoleConsentsAsync_WithoutAllRolesConsentGiven_ThrowsControllerArgumentException()
    {
        // Arrange
        var agreementIds1 = new Guid("0a283850-5a73-4940-9215-e713d0e1c419");
        var agreementIds2 = new Guid("e38da3a1-36f9-4002-9447-c55a38ac2a53");
        var consents = new CompanyRoleAgreementConsents(
            [
                CompanyRoleId.APP_PROVIDER,
            ],
            [
                new(agreementIds1, ConsentStatusId.ACTIVE),
                new(agreementIds2, ConsentStatusId.INACTIVE)
            ]);
        var applicationId = _fixture.Create<Guid>();
        var applicationStatusId = _fixture.Create<CompanyApplicationStatusId>();
        var data = new CompanyRoleAgreementConsentData(_identity.CompanyId, applicationStatusId, [CompanyRoleId.APP_PROVIDER], []);
        var companyRoleAssignedAgreements = new (CompanyRoleId CompanyRoleId, IEnumerable<AgreementStatusData> AgreementIds)[]
        {
            (CompanyRoleId.APP_PROVIDER, new AgreementStatusData[]{ new(agreementIds1, AgreementStatusId.ACTIVE), new(agreementIds2, AgreementStatusId.ACTIVE) })
        };
        A.CallTo(() => _companyRolesRepository.GetCompanyRoleAgreementConsentDataAsync(applicationId))
            .Returns(data);
        A.CallTo(() => _companyRolesRepository.GetAgreementAssignedCompanyRolesUntrackedAsync(A<IEnumerable<CompanyRoleId>>._))
            .Returns(companyRoleAssignedAgreements.ToAsyncEnumerable());

        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRoleConsentAsync(applicationId, consents);

        // Arrange
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_ARG_CONSENT_MUST_GIVEN_ALL_ASSIGNED_AGREEMENTS.ToString());
    }

    [Fact]
    public async Task SubmitRoleConsentsAsync_WithValidData_CallsExpected()
    {
        var agreementId_1 = _fixture.Create<Guid>();
        var agreementId_2 = _fixture.Create<Guid>();
        var agreementId_3 = _fixture.Create<Guid>();

        var consentId = _fixture.Create<Guid>();
        var now = DateTimeOffset.Now;

        IEnumerable<CompanyRoleId>? removedCompanyRoleIds = null;

        // Arrange
        var consents = new CompanyRoleAgreementConsents(
            [
                CompanyRoleId.APP_PROVIDER,
                CompanyRoleId.ACTIVE_PARTICIPANT
            ],
            [
                new AgreementConsentStatus(agreementId_1, ConsentStatusId.ACTIVE),
                new AgreementConsentStatus(agreementId_2, ConsentStatusId.ACTIVE)
            ]);
        var applicationId = _fixture.Create<Guid>();
        var applicationStatusId = CompanyApplicationStatusId.INVITE_USER;
        var application = _fixture.Build<CompanyApplication>()
            .With(x => x.Id, applicationId)
            .With(x => x.ApplicationStatusId, applicationStatusId)
            .Create();
        var data = new CompanyRoleAgreementConsentData(
            _identity.CompanyId,
            applicationStatusId,
            [
                CompanyRoleId.APP_PROVIDER,
                CompanyRoleId.SERVICE_PROVIDER,
            ],
            [
                new ConsentData(consentId, ConsentStatusId.INACTIVE, agreementId_1)
            ]);
        var companyRoleAssignedAgreements = new (CompanyRoleId CompanyRoleId, IEnumerable<AgreementStatusData> AgreementIds)[]
        {
            (CompanyRoleId.APP_PROVIDER,
                new AgreementStatusData[] { new(agreementId_1, AgreementStatusId.ACTIVE), new(agreementId_2, AgreementStatusId.ACTIVE) }),
            (CompanyRoleId.ACTIVE_PARTICIPANT,
                new AgreementStatusData[] { new(agreementId_1, AgreementStatusId.INACTIVE) }),
            (CompanyRoleId.SERVICE_PROVIDER,
                new AgreementStatusData[] { new(agreementId_1, AgreementStatusId.INACTIVE), new(agreementId_3, AgreementStatusId.INACTIVE) }),
        };
        A.CallTo(() => _dateTimeProvider.OffsetNow).Returns(now);
        A.CallTo(() => _companyRolesRepository.GetCompanyRoleAgreementConsentDataAsync(applicationId))
            .Returns(data);
        A.CallTo(() => _companyRolesRepository.GetAgreementAssignedCompanyRolesUntrackedAsync(A<IEnumerable<CompanyRoleId>>._))
            .Returns(companyRoleAssignedAgreements.ToAsyncEnumerable());
        A.CallTo(() => _consentRepository.AttachAndModifiesConsents(A<IEnumerable<Guid>>._, A<Action<Consent>>._))
            .Invokes((IEnumerable<Guid> consentIds, Action<Consent> setOptionalParameter) =>
            {
                var consents = consentIds.Select(x => new Consent(x, Guid.Empty, Guid.Empty, Guid.Empty, default, default));
                foreach (var consent in consents)
                {
                    setOptionalParameter.Invoke(consent);
                }
            });
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(A<Guid>._, A<Action<CompanyApplication>>._))
            .Invokes((Guid companyApplicationId, Action<CompanyApplication> setOptionalParameters) =>
            {
                setOptionalParameters.Invoke(application);
            });
        A.CallTo(() => _companyRolesRepository.RemoveCompanyAssignedRoles(_identity.CompanyId, A<IEnumerable<CompanyRoleId>>._))
            .Invokes((Guid _, IEnumerable<CompanyRoleId> companyRoleIds) =>
            {
                removedCompanyRoleIds = companyRoleIds;
            });

        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        await sut.SubmitRoleConsentAsync(applicationId, consents);

        // Arrange
        A.CallTo(() => _consentRepository.AttachAndModifiesConsents(A<IEnumerable<Guid>>._, A<Action<Consent>>._)).MustHaveHappened(2, Times.Exactly);
        A.CallTo(() => _consentRepository.CreateConsent(A<Guid>._, A<Guid>._, A<Guid>._, A<ConsentStatusId>._, A<Action<Consent>?>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _companyRolesRepository.CreateCompanyAssignedRole(_identity.CompanyId, CompanyRoleId.ACTIVE_PARTICIPANT)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _companyRolesRepository.CreateCompanyAssignedRole(A<Guid>.That.Not.IsEqualTo(_identity.CompanyId), A<CompanyRoleId>._)).MustNotHaveHappened();
        A.CallTo(() => _companyRolesRepository.CreateCompanyAssignedRole(A<Guid>._, A<CompanyRoleId>.That.Not.IsEqualTo(CompanyRoleId.ACTIVE_PARTICIPANT))).MustNotHaveHappened();
        A.CallTo(() => _companyRolesRepository.RemoveCompanyAssignedRoles(_identity.CompanyId, A<IEnumerable<CompanyRoleId>>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _companyRolesRepository.RemoveCompanyAssignedRoles(A<Guid>.That.Not.IsEqualTo(_identity.CompanyId), A<IEnumerable<CompanyRoleId>>._)).MustNotHaveHappened();
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(A<Guid>._, A<Action<CompanyApplication>>._)).MustHaveHappenedOnceExactly();
        removedCompanyRoleIds.Should().NotBeNull();
        removedCompanyRoleIds.Should().ContainSingle(x => x == CompanyRoleId.SERVICE_PROVIDER);
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
        application.DateLastChanged.Should().Be(now);
        application.ApplicationStatusId.Should().Be(CompanyApplicationStatusId.UPLOAD_DOCUMENTS);
    }

    [Fact]
    public async Task SubmitRoleConsentsAsync_WithextraAgreement_ThrowsControllerArgumentException()
    {
        // Arrange
        var agreementIds1 = new Guid("e38da3a1-36f9-4002-9447-c55a38ac2a53");
        var agreementIds2 = new Guid("0a283850-5a73-4940-9215-e713d0e1c419");
        var agreementIds3 = new Guid("e38da3a1-36f9-4002-9447-c55a38ac2a54");
        var consents = new CompanyRoleAgreementConsents(
            [
                CompanyRoleId.APP_PROVIDER,
            ],
            [
                new AgreementConsentStatus(agreementIds1, ConsentStatusId.ACTIVE),
                new AgreementConsentStatus(agreementIds2, ConsentStatusId.ACTIVE),
                new AgreementConsentStatus(agreementIds3, ConsentStatusId.ACTIVE)
            ]);
        var applicationId = _fixture.Create<Guid>();
        var applicationStatusId = _fixture.Create<CompanyApplicationStatusId>();

        var data = new CompanyRoleAgreementConsentData(_identity.CompanyId, applicationStatusId, [CompanyRoleId.APP_PROVIDER], []);
        var companyRoleAssignedAgreements = new (CompanyRoleId CompanyRoleId, IEnumerable<AgreementStatusData> AgreementIds)[]
        {
            (CompanyRoleId.APP_PROVIDER,
                new AgreementStatusData[]{
                    new(agreementIds1, AgreementStatusId.ACTIVE),
                    new(agreementIds2, AgreementStatusId.ACTIVE)})
        };
        A.CallTo(() => _companyRolesRepository.GetCompanyRoleAgreementConsentDataAsync(applicationId))
            .Returns(data);
        A.CallTo(() => _companyRolesRepository.GetAgreementAssignedCompanyRolesUntrackedAsync(A<IEnumerable<CompanyRoleId>>._))
            .Returns(companyRoleAssignedAgreements.ToAsyncEnumerable());

        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRoleConsentAsync(applicationId, consents);

        // Arrange
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_ARG_AGREEMENTS_NOT_ASSOCIATED_COMPANY_ROLES.ToString());
    }

    #endregion

    #region SubmitRegistrationAsync

    [Fact]
    public async Task SubmitRegistrationAsync_WithNotExistingApplication_ThrowsNotFoundException()
    {
        // Arrange
        var notExistingId = _fixture.Create<Guid>();
        var settings = new RegistrationSettings
        {
            SubmitDocumentTypeIds = [
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(A<Guid>._, A<Guid>._, A<IEnumerable<DocumentTypeId>>._))
            .Returns<CompanyApplicationUserEmailData?>(null);
        var sut = new RegistrationBusinessLogic(Options.Create(settings), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRegistrationAsync(notExistingId);

        // Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_APPLICATION_NOT_EXIST.ToString());
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(notExistingId, _identity.IdentityId, A<IEnumerable<DocumentTypeId>>.That.IsSameSequenceAs(new[] { DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT })))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SubmitRegistrationAsync_WithDocumentId_Success()
    {
        // Arrange
        var applicationId = _fixture.Create<Guid>();
        var now = DateTimeOffset.Now;
        var documents = new DocumentStatusData[] {
            new(Guid.NewGuid(),DocumentStatusId.PENDING),
            new(Guid.NewGuid(),DocumentStatusId.INACTIVE)
        };
        var checklist = _fixture.CreateMany<ApplicationChecklistEntryTypeId>(3).Select(x => (x, ApplicationChecklistEntryStatusId.TO_DO)).ToImmutableArray();
        var stepTypeIds = _fixture.CreateMany<ProcessStepTypeId>(3).ToImmutableArray();
        var uniqueIds = _fixture.CreateMany<UniqueIdentifierId>(3).ToImmutableArray();
        var companyRoleIds = _fixture.CreateMany<CompanyRoleId>(3).ToImmutableArray();
        var agreementConsents = new[]
        {
            (Guid.NewGuid(), ConsentStatusId.ACTIVE),
        };
        var companyData = new CompanyData("Test Company", Guid.NewGuid(), "Strabe Street", "Munich", "Germany", uniqueIds, companyRoleIds);
        A.CallTo(() => _dateTimeProvider.OffsetNow).Returns(now);
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(A<Guid>._, A<Guid>._, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(CompanyApplicationStatusId.VERIFY, true, "test@mail.de", documents, companyData, agreementConsents));

        var modifiedDocuments = new List<(Document Initial, Document Modified)>();

        A.CallTo(() => _documentRepository.AttachAndModifyDocuments(A<IEnumerable<(Guid DocumentId, Action<Document>?, Action<Document>)>>._))
            .Invokes((IEnumerable<(Guid DocumentId, Action<Document>? Initialize, Action<Document> Modify)> documentKeyActions) =>
            {
                foreach (var x in documentKeyActions)
                {
                    var initial = new Document(x.DocumentId, null!, null!, null!, default, default, default, default, default);
                    x.Initialize?.Invoke(initial);
                    var modified = new Document(x.DocumentId, null!, null!, null!, default, default, default, default, default);
                    x.Modify(modified);
                    modifiedDocuments.Add((initial, modified));
                }
            });

        A.CallTo(() => _checklistService.CreateInitialChecklistAsync(applicationId))
            .Returns(checklist);

        A.CallTo(() => _checklistService.GetInitialProcessStepTypeIds(A<IEnumerable<(ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId)>>.That.IsSameSequenceAs(checklist)))
            .Returns(stepTypeIds);

        var utcNow = DateTimeOffset.UtcNow;

        Process? process = null;

        A.CallTo(() => _processStepRepository.CreateProcess(ProcessTypeId.APPLICATION_CHECKLIST))
            .ReturnsLazily((ProcessTypeId processTypeId) =>
            {
                process = new Process(Guid.NewGuid(), processTypeId, Guid.NewGuid());
                return process;
            });

        CompanyApplication? application = null;

        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(A<Guid>._, A<Action<CompanyApplication>>._))
            .Invokes((Guid applicationId, Action<CompanyApplication> setOptionalParameters) =>
            {
                application = new CompanyApplication(applicationId, Guid.Empty, default, default, default);
                setOptionalParameters(application);
            });

        IEnumerable<ProcessStep<Process, ProcessTypeId, ProcessStepTypeId>>? processSteps = null;

        A.CallTo(() => _processStepRepository.CreateProcessStepRange(A<IEnumerable<(ProcessStepTypeId, ProcessStepStatusId, Guid)>>._))
            .ReturnsLazily((IEnumerable<(ProcessStepTypeId ProcessStepTypeId, ProcessStepStatusId ProcessStepStatusId, Guid ProcessId)> processStepTypeStatus) =>
            {
                processSteps = processStepTypeStatus.Select(x => new ProcessStep<Process, ProcessTypeId, ProcessStepTypeId>(Guid.NewGuid(), x.ProcessStepTypeId, x.ProcessStepStatusId, x.ProcessId, utcNow)).ToImmutableArray();
                return processSteps;
            });
        var settings = new RegistrationSettings
        {
            SubmitDocumentTypeIds = [
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        var sut = new RegistrationBusinessLogic(Options.Create(settings), null!, null!, null!, null!, _portalRepositories, _checklistService, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        await sut.SubmitRegistrationAsync(applicationId);

        // Assert
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, _identity.IdentityId, A<IEnumerable<DocumentTypeId>>.That.IsSameSequenceAs(new[] { DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT })))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _documentRepository.AttachAndModifyDocuments(A<IEnumerable<(Guid DocumentId, Action<Document>?, Action<Document>)>>.That.Matches(x => x.Count() == 2)))
            .MustHaveHappenedOnceExactly();

        modifiedDocuments.Should().HaveCount(2).And.Satisfy(
            x => x.Initial.Id == documents[0].DocumentId && x.Initial.DocumentStatusId == documents[0].StatusId && x.Modified.Id == documents[0].DocumentId && x.Modified.DocumentStatusId == DocumentStatusId.LOCKED,
            x => x.Initial.Id == documents[1].DocumentId && x.Initial.DocumentStatusId == documents[1].StatusId && x.Modified.Id == documents[1].DocumentId && x.Modified.DocumentStatusId == DocumentStatusId.LOCKED
        );

        A.CallTo(() => _checklistService.CreateInitialChecklistAsync(applicationId))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _checklistService.GetInitialProcessStepTypeIds(A<IEnumerable<(ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId)>>.That.IsSameSequenceAs(checklist)))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _processStepRepository.CreateProcess(ProcessTypeId.APPLICATION_CHECKLIST))
            .MustHaveHappenedOnceExactly();

        process.Should().NotBeNull();
        process!.ProcessTypeId.Should().Be(ProcessTypeId.APPLICATION_CHECKLIST);

        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(A<Guid>._, A<Action<CompanyApplication>>._))
            .MustHaveHappenedOnceExactly();

        application.Should().NotBeNull();
        application!.ChecklistProcessId.Should().Be(process!.Id);
        application.ApplicationStatusId.Should().Be(CompanyApplicationStatusId.SUBMITTED);
        application.DateLastChanged.Should().Be(now);

        A.CallTo(() => _processStepRepository.CreateProcessStepRange(A<IEnumerable<(ProcessStepTypeId, ProcessStepStatusId, Guid)>>._))
            .MustHaveHappenedOnceExactly();

        processSteps.Should().NotBeNull()
            .And.HaveCount(stepTypeIds.Length)
            .And.AllSatisfy(x =>
                {
                    x.ProcessId.Should().Be(process.Id);
                    x.ProcessStepStatusId.Should().Be(ProcessStepStatusId.TODO);
                })
            .And.Satisfy(
                x => x.ProcessStepTypeId == stepTypeIds[0],
                x => x.ProcessStepTypeId == stepTypeIds[1],
                x => x.ProcessStepTypeId == stepTypeIds[2]
            );

        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(CompanyApplicationStatusId.CREATED)]
    [InlineData(CompanyApplicationStatusId.ADD_COMPANY_DATA)]
    [InlineData(CompanyApplicationStatusId.INVITE_USER)]
    [InlineData(CompanyApplicationStatusId.SELECT_COMPANY_ROLE)]
    [InlineData(CompanyApplicationStatusId.UPLOAD_DOCUMENTS)]
    public async Task SubmitRegistrationAsync_InvalidStatus_ThrowsForbiddenException(CompanyApplicationStatusId statusId)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(userId);
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var applicationId = _fixture.Create<Guid>();
        var uniqueIds = _fixture.CreateMany<UniqueIdentifierId>(3).ToImmutableArray();
        var companyRoleIds = _fixture.CreateMany<CompanyRoleId>(3).ToImmutableArray();
        var documents = new DocumentStatusData[] {
            new(Guid.NewGuid(),DocumentStatusId.PENDING),
            new(Guid.NewGuid(),DocumentStatusId.INACTIVE)
        };
        var agreementConsents = new[]
        {
            (Guid.NewGuid(), ConsentStatusId.ACTIVE),
        };
        var settings = new RegistrationSettings
        {
            SubmitDocumentTypeIds = [
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        var companyData = new CompanyData("Test Company", Guid.NewGuid(), "Strabe Street", "Munich", "Germany", uniqueIds, companyRoleIds);
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(A<Guid>._, A<Guid>._, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(statusId, true, _fixture.Create<string>(), documents, companyData, agreementConsents));
        var sut = new RegistrationBusinessLogic(Options.Create(settings), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRegistrationAsync(applicationId);

        // Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_STATUS_NOT_FITTING_PRE_REQUITISE.ToString());
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, userId, A<IEnumerable<DocumentTypeId>>.That.IsSameSequenceAs(new[] { DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT })))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(CompanyApplicationStatusId.SUBMITTED)]
    [InlineData(CompanyApplicationStatusId.CONFIRMED)]
    [InlineData(CompanyApplicationStatusId.DECLINED)]
    public async Task SubmitRegistrationAsync_AlreadyClosed_ThrowsForbiddenException(CompanyApplicationStatusId statusId)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(userId);
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var applicationId = _fixture.Create<Guid>();
        var uniqueIds = _fixture.CreateMany<UniqueIdentifierId>(3).ToImmutableArray();
        var companyRoleIds = _fixture.CreateMany<CompanyRoleId>(3).ToImmutableArray();
        var documents = new DocumentStatusData[] {
            new(Guid.NewGuid(),DocumentStatusId.PENDING),
            new(Guid.NewGuid(),DocumentStatusId.INACTIVE)
        };
        var agreementConsents = new[]
        {
            (Guid.NewGuid(), ConsentStatusId.ACTIVE),
        };
        var companyData = new CompanyData("Test Company", Guid.NewGuid(), "Strabe Street", "Munich", "Germany", uniqueIds, companyRoleIds);
        var settings = new RegistrationSettings
        {
            SubmitDocumentTypeIds = [
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(A<Guid>._, A<Guid>._, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(statusId, true, _fixture.Create<string>(), documents, companyData, agreementConsents));
        var sut = new RegistrationBusinessLogic(Options.Create(settings), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRegistrationAsync(applicationId);

        // Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_APPLICATION_ALREADY_CLOSED.ToString());
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, userId, A<IEnumerable<DocumentTypeId>>.That.IsSameSequenceAs(new[] { DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT })))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SubmitRegistrationAsync_WithNotExistingCompanyUser_ThrowsForbiddenException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(userId);
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var applicationId = _fixture.Create<Guid>();
        var uniqueIds = _fixture.CreateMany<UniqueIdentifierId>(3).ToImmutableArray();
        var companyRoleIds = _fixture.CreateMany<CompanyRoleId>(3).ToImmutableArray();
        var agreementConsents = new[]
        {
            (Guid.NewGuid(), ConsentStatusId.ACTIVE),
        };
        var companyData = new CompanyData("Test Company", Guid.NewGuid(), "Strabe Street", "Munich", "Germany", uniqueIds, companyRoleIds);
        var settings = new RegistrationSettings
        {
            SubmitDocumentTypeIds = [
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(A<Guid>._, A<Guid>._, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(CompanyApplicationStatusId.VERIFY, false, null, null!, companyData, agreementConsents));
        var sut = new RegistrationBusinessLogic(Options.Create(settings), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRegistrationAsync(applicationId);

        // Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_USER_ID_NOT_ASSOCIATED_WITH_COMPANY_APPLICATION.ToString());
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, userId, A<IEnumerable<DocumentTypeId>>.That.IsSameSequenceAs(new[] { DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT })))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SubmitRegistrationAsync_WithNotExistingStreetName_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(userId);
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var applicationId = _fixture.Create<Guid>();
        var uniqueIds = _fixture.CreateMany<UniqueIdentifierId>(3).ToImmutableArray();
        var companyRoleIds = _fixture.CreateMany<CompanyRoleId>(3).ToImmutableArray();
        var agreementConsents = new[]
        {
            (Guid.NewGuid(), ConsentStatusId.ACTIVE),
        };
        var companyData = new CompanyData("Test Company", Guid.NewGuid(), string.Empty, "Munich", "Germany", uniqueIds, companyRoleIds);
        var settings = new RegistrationSettings
        {
            SubmitDocumentTypeIds = [
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, userId, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(CompanyApplicationStatusId.VERIFY, true, _fixture.Create<string>(), Enumerable.Empty<DocumentStatusData>(), companyData, agreementConsents));
        var sut = new RegistrationBusinessLogic(Options.Create(settings), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRegistrationAsync(applicationId);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_CONFLICT_STREET_NOT_EMPTY.ToString());
    }

    [Fact]
    public async Task SubmitRegistrationAsync_WithNotExistingAddressId_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(userId);
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var applicationId = _fixture.Create<Guid>();
        var uniqueIds = _fixture.CreateMany<UniqueIdentifierId>(3).ToImmutableArray();
        var companyRoleIds = _fixture.CreateMany<CompanyRoleId>(3).ToImmutableArray();
        var agreementConsents = new[]
        {
            (Guid.NewGuid(), ConsentStatusId.ACTIVE),
        };
        var companyData = new CompanyData("Test Company", null, "Strabe Street", "Munich", "Germany", uniqueIds, companyRoleIds);
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, userId, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(CompanyApplicationStatusId.VERIFY, true, _fixture.Create<string>(), Enumerable.Empty<DocumentStatusData>(), companyData, agreementConsents));
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRegistrationAsync(applicationId);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_CONFLICT_ADDRESS_NOT_EMPTY.ToString());
    }

    [Fact]
    public async Task SubmitRegistrationAsync_WithNotExistingCompanyName_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(userId);
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var applicationId = _fixture.Create<Guid>();
        var uniqueIds = _fixture.CreateMany<UniqueIdentifierId>(3).ToImmutableArray();
        var companyRoleIds = _fixture.CreateMany<CompanyRoleId>(3).ToImmutableArray();
        var agreementConsents = new[]
        {
            (Guid.NewGuid(), ConsentStatusId.ACTIVE),
        };
        var companyData = new CompanyData(string.Empty, Guid.NewGuid(), "Strabe Street", "Munich", "Germany", uniqueIds, companyRoleIds);
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, userId, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(CompanyApplicationStatusId.VERIFY, true, _fixture.Create<string>(), Enumerable.Empty<DocumentStatusData>(), companyData, agreementConsents));
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRegistrationAsync(applicationId);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_CONFLICT_COMPANY_NAME_NOT_EMPTY.ToString());
    }

    [Fact]
    public async Task SubmitRegistrationAsync_WithNotExistingUniqueId_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(userId);
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var applicationId = _fixture.Create<Guid>();
        var companyRoleIds = _fixture.CreateMany<CompanyRoleId>(3).ToImmutableArray();
        var agreementConsents = new[]
        {
            (Guid.NewGuid(), ConsentStatusId.ACTIVE),
        };
        var uniqueIdentifierData = Enumerable.Empty<UniqueIdentifierId>();
        var companyData = new CompanyData("Test Company", Guid.NewGuid(), "Strabe Street", "Munich", "Germany", uniqueIdentifierData, companyRoleIds);
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, userId, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(CompanyApplicationStatusId.VERIFY, true, _fixture.Create<string>(), Enumerable.Empty<DocumentStatusData>(), companyData, agreementConsents));
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRegistrationAsync(applicationId);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_CONFLICT_COMPANY_IDENTIFIERS_NOT_EMPTY.ToString());
    }

    [Fact]
    public async Task SubmitRegistrationAsync_WithNotExistingCompanyRoleId_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(userId);
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var applicationId = _fixture.Create<Guid>();
        var uniqueIds = _fixture.CreateMany<UniqueIdentifierId>(3).ToImmutableArray();
        var agreementConsents = new[]
        {
            (Guid.NewGuid(), ConsentStatusId.ACTIVE),
        };
        var companyRoleIdData = Enumerable.Empty<CompanyRoleId>();
        var companyData = new CompanyData("Test Company", Guid.NewGuid(), "Strabe Street", "Munich", "Germany", uniqueIds, companyRoleIdData);
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, userId, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(CompanyApplicationStatusId.VERIFY, true, _fixture.Create<string>(), Enumerable.Empty<DocumentStatusData>(), companyData, agreementConsents));
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRegistrationAsync(applicationId);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_CONFLICT_COMPANY_ASSIGNED_ROLE_NOT_EMPTY.ToString());
    }

    [Fact]
    public async Task SubmitRegistrationAsync_WithNotExistingAgreementandConsent_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(userId);
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var applicationId = _fixture.Create<Guid>();
        var uniqueIds = _fixture.CreateMany<UniqueIdentifierId>(3).ToImmutableArray();
        var companyRoleIds = _fixture.CreateMany<CompanyRoleId>(3).ToImmutableArray();
        var agreementConsents = Enumerable.Empty<(Guid, ConsentStatusId)>();
        var companyData = new CompanyData("Test Company", Guid.NewGuid(), "Strabe Street", "Munich", "Germany", uniqueIds, companyRoleIds);
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, userId, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(CompanyApplicationStatusId.VERIFY, true, _fixture.Create<string>(), Enumerable.Empty<DocumentStatusData>(), companyData, agreementConsents));
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRegistrationAsync(applicationId);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_CONFLICT_AGREE_CONSENT_NOT_EMPTY.ToString());
    }

    [Fact]
    public async Task SubmitRegistrationAsync_WithNotExistingCity_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(userId);
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var applicationId = _fixture.Create<Guid>();
        var uniqueIds = _fixture.CreateMany<UniqueIdentifierId>(3).ToImmutableArray();
        var companyRoleIds = _fixture.CreateMany<CompanyRoleId>(3).ToImmutableArray();
        var agreementConsents = new[]
        {
            (Guid.NewGuid(), ConsentStatusId.ACTIVE),
        };
        var companyData = new CompanyData("Test Company", Guid.NewGuid(), "Strabe Street", string.Empty, "Germany", uniqueIds, companyRoleIds);
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, userId, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(CompanyApplicationStatusId.VERIFY, true, _fixture.Create<string>(), Enumerable.Empty<DocumentStatusData>(), companyData, agreementConsents));
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRegistrationAsync(applicationId);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_CONFLICT_CITY_NOT_EMPTY.ToString());
    }

    [Fact]
    public async Task SubmitRegistrationAsync_WithNotExistingCountry_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identityData = A.Fake<IIdentityData>();
        A.CallTo(() => identityData.IdentityId).Returns(userId);
        A.CallTo(() => identityData.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => identityData.CompanyId).Returns(Guid.NewGuid());
        A.CallTo(() => _identityService.IdentityData).Returns(identityData);
        var applicationId = _fixture.Create<Guid>();
        var uniqueIds = _fixture.CreateMany<UniqueIdentifierId>(3).ToImmutableArray();
        var companyRoleIds = _fixture.CreateMany<CompanyRoleId>(3).ToImmutableArray();
        var agreementConsents = new[]
        {
            (Guid.NewGuid(), ConsentStatusId.ACTIVE),
        };

        var companyData = new CompanyData("Test Company", Guid.NewGuid(), "Strabe Street", "Munich", string.Empty, uniqueIds, companyRoleIds);
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, userId, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(CompanyApplicationStatusId.VERIFY, true, _fixture.Create<string>(), Enumerable.Empty<DocumentStatusData>(), companyData, agreementConsents));
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.SubmitRegistrationAsync(applicationId);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_CONFLICT_COUNTRY_NOT_EMPTY.ToString());
    }

    [Fact]
    public async Task SubmitRegistrationAsync_WithUserEmail_SendsMail()
    {
        // Arrange
        var applicationId = _fixture.Create<Guid>();
        var now = DateTimeOffset.Now;
        var uniqueIds = _fixture.CreateMany<UniqueIdentifierId>(3).ToImmutableArray();
        var companyRoleIds = _fixture.CreateMany<CompanyRoleId>(3).ToImmutableArray();
        var agreementConsents = new[]
        {
            (Guid.NewGuid(), ConsentStatusId.ACTIVE),
        };
        IEnumerable<DocumentStatusData> documents = [
            new(
                Guid.NewGuid(), DocumentStatusId.INACTIVE
            )];
        var companyData = new CompanyData("Test Company", Guid.NewGuid(), "Strabe Street", "Munich", "Germany", uniqueIds, companyRoleIds);
        var settings = new RegistrationSettings
        {
            SubmitDocumentTypeIds = [
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        var application = _fixture.Build<CompanyApplication>()
            .With(x => x.Id, applicationId)
            .Create();
        A.CallTo(() => _dateTimeProvider.OffsetNow).Returns(now);
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(A<Guid>._, A<Guid>._, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(CompanyApplicationStatusId.VERIFY, true, "test@mail.de", documents, companyData, agreementConsents));
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(applicationId, A<Action<CompanyApplication>>._))
            .Invokes((Guid _, Action<CompanyApplication> setOptionalFields) =>
            {
                setOptionalFields.Invoke(application);
            });
        var sut = new RegistrationBusinessLogic(Options.Create(settings), null!, null!, null!, null!, _portalRepositories, _checklistService, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        var result = await sut.SubmitRegistrationAsync(applicationId);

        // Assert
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, _identity.IdentityId, A<IEnumerable<DocumentTypeId>>.That.IsSameSequenceAs(new[] { DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT })))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(applicationId, A<Action<CompanyApplication>>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _checklistService.CreateInitialChecklistAsync(applicationId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _mailingProcessCreation.CreateMailProcess(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>>._))
            .MustHaveHappened();
        result.Should().BeTrue();
        application.DateLastChanged.Should().Be(now);
    }

    [Fact]
    public async Task SubmitRegistrationAsync_WithoutUserEmail_DoesntSendMail()
    {
        // Arrange
        var applicationId = _fixture.Create<Guid>();
        var now = DateTimeOffset.Now;
        var uniqueIds = _fixture.CreateMany<UniqueIdentifierId>(3).ToImmutableArray();
        var companyRoleIds = _fixture.CreateMany<CompanyRoleId>(3).ToImmutableArray();
        var agreementConsents = new (Guid AgreementId, ConsentStatusId ConsentStatusId)[]
        {
            (Guid.NewGuid(), ConsentStatusId.ACTIVE),
        };
        IEnumerable<DocumentStatusData> documents = [
            new(
                Guid.NewGuid(), DocumentStatusId.PENDING
            )];
        var companyData = new CompanyData("Test Company", Guid.NewGuid(), "Strabe Street", "Munich", "Germany", uniqueIds, companyRoleIds);
        var settings = new RegistrationSettings
        {
            SubmitDocumentTypeIds = [
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        var application = _fixture.Build<CompanyApplication>()
            .With(x => x.Id, applicationId)
            .Create();
        A.CallTo(() => _dateTimeProvider.OffsetNow).Returns(now);
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(A<Guid>._, A<Guid>._, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new CompanyApplicationUserEmailData(CompanyApplicationStatusId.VERIFY, true, null, documents, companyData, agreementConsents));
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(applicationId, A<Action<CompanyApplication>>._))
            .Invokes((Guid _, Action<CompanyApplication> setOptionalFields) =>
            {
                setOptionalFields.Invoke(application);
            });
        var sut = new RegistrationBusinessLogic(Options.Create(settings), null!, null!, null!, A.Fake<ILogger<RegistrationBusinessLogic>>(), _portalRepositories, _checklistService, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        var result = await sut.SubmitRegistrationAsync(applicationId);

        // Assert
        A.CallTo(() => _applicationRepository.GetOwnCompanyApplicationUserEmailDataAsync(applicationId, _identity.IdentityId, A<IEnumerable<DocumentTypeId>>.That.IsSameSequenceAs(new[] { DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT })))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(applicationId, A<Action<CompanyApplication>>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _checklistService.CreateInitialChecklistAsync(applicationId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _mailingProcessCreation.CreateMailProcess(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>>._))
            .MustNotHaveHappened();
        result.Should().BeTrue();
        application.DateLastChanged.Should().Be(now);
    }

    #endregion

    #region GetCompanyIdentifiers

    [Fact]
    public async Task GetCompanyIdentifiers_ReturnsExpectedOutput()
    {
        // Arrange
        var uniqueIdentifierData = _fixture.CreateMany<UniqueIdentifierId>();

        A.CallTo(() => _staticDataRepository.GetCompanyIdentifiers(A<string>._))
            .Returns((uniqueIdentifierData, true));

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        // Act
        var result = await sut.GetCompanyIdentifiers(_fixture.Create<string>());

        // Assert
        result.Should().NotBeNull();
        foreach (var item in result)
        {
            A.CallTo(() => _staticDataRepository.GetCompanyIdentifiers(A<string>._)).MustHaveHappenedOnceExactly();
            Assert.NotNull(item);
            Assert.IsType<UniqueIdentifierData?>(item);
        }
    }

    [Fact]
    public async Task GetCompanyIdentifiers_InvalidCountry_Throws()
    {
        // Arrange
        A.CallTo(() => _staticDataRepository.GetCompanyIdentifiers(A<string>._))
            .Returns<(IEnumerable<UniqueIdentifierId>, bool)>(default);

        var sut = new RegistrationBusinessLogic(
            _options,
            null!,
            null!,
            null!,
            null!,
            _portalRepositories,
            null!,
            _identityService,
            _dateTimeProvider,
            _mailingProcessCreation);

        var countryCode = _fixture.Create<string>();

        // Act
        Task Act() => sut.GetCompanyIdentifiers(countryCode);

        // Assert
        var result = await Assert.ThrowsAsync<NotFoundException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_NOT_INVALID_COUNTRY_CODE.ToString());
    }

    #endregion

    #region GetRegistrationDataAsync

    [Fact]
    public async Task GetRegistrationDataAsync_ReturnsExpected()
    {
        // Arrange
        var data = _fixture.Create<RegistrationData>();
        A.CallTo(() => _applicationRepository.GetRegistrationDataUntrackedAsync(_existingApplicationId, _identity.CompanyId, A<IEnumerable<DocumentTypeId>>._))
            .Returns((true, true, data));

        var sut = new RegistrationBusinessLogic(_options, null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        var result = await sut.GetRegistrationDataAsync(_existingApplicationId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<CompanyRegistrationData>();
        result.Should().Match<CompanyRegistrationData>(x =>
            x.CompanyId == data.CompanyId &&
            x.Name == data.Name &&
            x.BusinessPartnerNumber == data.BusinessPartnerNumber &&
            x.ShortName == data.ShortName &&
            x.City == data.City &&
            x.Region == data.Region &&
            x.StreetAdditional == data.StreetAdditional &&
            x.StreetName == data.StreetName &&
            x.StreetNumber == data.StreetNumber &&
            x.ZipCode == data.ZipCode &&
            x.CountryAlpha2Code == data.CountryAlpha2Code);
        result.CompanyRoleIds.Should().HaveSameCount(data.CompanyRoleIds);
        result.CompanyRoleIds.Should().ContainInOrder(data.CompanyRoleIds);
        result.AgreementConsentStatuses.Should().HaveSameCount(data.AgreementConsentStatuses);
        result.AgreementConsentStatuses.Zip(data.AgreementConsentStatuses).Should().AllSatisfy(x =>
            x.Should().Match<(AgreementConsentStatusForRegistrationData First, (Guid AgreementId, ConsentStatusId ConsentStatusId) Second)>(x =>
                x.First.AgreementId == x.Second.AgreementId && x.First.ConsentStatusId == x.Second.ConsentStatusId));
        result.Documents.Should().HaveSameCount(data.DocumentNames);
        result.Documents.Zip(data.DocumentNames).Should().AllSatisfy(x =>
            x.Should().Match<(RegistrationDocumentNames First, string Second)>(x =>
                x.First.DocumentName == x.Second));
        result.UniqueIds.Should().HaveSameCount(data.Identifiers);
        result.UniqueIds.Zip(data.Identifiers).Should().AllSatisfy(x =>
            x.Should().Match<(CompanyUniqueIdData First, (UniqueIdentifierId UniqueIdentifierId, string Value) Second)>(x =>
                x.First.UniqueIdentifierId == x.Second.UniqueIdentifierId && x.First.Value == x.Second.Value));
    }

    [Fact]
    public async Task GetRegistrationDataAsync_WithInvalidApplicationId_Throws()
    {
        // Arrange
        var data = _fixture.Create<RegistrationData>();
        var applicationId = Guid.NewGuid();
        A.CallTo(() => _applicationRepository.GetRegistrationDataUntrackedAsync(A<Guid>._, _identity.CompanyId, A<IEnumerable<DocumentTypeId>>._))
            .Returns((false, false, data));

        var sut = new RegistrationBusinessLogic(_options, null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.GetRegistrationDataAsync(applicationId);

        // Assert
        var result = await Assert.ThrowsAsync<NotFoundException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_APPLICATION_NOT_EXIST.ToString());
    }

    [Fact]
    public async Task GetRegistrationDataAsync_WithInvalidUser_Throws()
    {
        // Arrange
        var data = _fixture.Create<RegistrationData>();
        var applicationId = Guid.NewGuid();
        A.CallTo(() => _applicationRepository.GetRegistrationDataUntrackedAsync(A<Guid>._, _identity.CompanyId, A<IEnumerable<DocumentTypeId>>._))
            .Returns((true, false, data));

        var sut = new RegistrationBusinessLogic(_options, null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.GetRegistrationDataAsync(applicationId);

        // Assert
        var result = await Assert.ThrowsAsync<ForbiddenException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_USER_APPLICATION_NOT_ASSIGN_WITH_COMP_APPLICATION.ToString());
    }

    [Fact]
    public async Task GetRegistrationDataAsync_WithNullData_Throws()
    {
        var applicationId = Guid.NewGuid();

        // Arrange
        A.CallTo(() => _applicationRepository.GetRegistrationDataUntrackedAsync(A<Guid>._, _identity.CompanyId, A<IEnumerable<DocumentTypeId>>._))
            .Returns((true, true, null));

        var sut = new RegistrationBusinessLogic(_options, null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.GetRegistrationDataAsync(applicationId);

        // Assert
        var result = await Assert.ThrowsAsync<UnexpectedConditionException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_UNEXPECT_REGISTER_DATA_NOT_NULL_APPLICATION.ToString());
    }

    #endregion

    [Fact]
    public async Task GetRegistrationDocumentAsync_ReturnsExpectedResult()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var content = new byte[7];
        A.CallTo(() => _documentRepository.GetDocumentAsync(documentId, A<IEnumerable<DocumentTypeId>>._))
            .Returns((content, "test.json", true, MediaTypeId.JSON));
        var sut = new RegistrationBusinessLogic(_options, null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        //Act
        var result = await sut.GetRegistrationDocumentAsync(documentId);

        // Assert
        A.CallTo(() => _documentRepository.GetDocumentAsync(documentId, A<IEnumerable<DocumentTypeId>>._)).MustHaveHappenedOnceExactly();
        result.Should().NotBeNull();
        result.fileName.Should().Be("test.json");
    }

    [Fact]
    public async Task GetRegistrationDocumentAsync_WithInvalidDocumentTypeId_ThrowsNotFoundException()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var content = new byte[7];
        A.CallTo(() => _documentRepository.GetDocumentAsync(documentId, A<IEnumerable<DocumentTypeId>>._))
            .Returns((content, "test.json", false, MediaTypeId.JSON));
        var sut = new RegistrationBusinessLogic(_options, null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        //Act
        Task Act() => sut.GetRegistrationDocumentAsync(documentId);

        // Assert
        var result = await Assert.ThrowsAsync<NotFoundException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_DOCUMENT_NOT_EXIST.ToString());
    }

    [Fact]
    public async Task GetRegistrationDocumentAsync_WithInvalidDocumentId_ThrowsNotFoundException()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        A.CallTo(() => _documentRepository.GetDocumentAsync(documentId, A<IEnumerable<DocumentTypeId>>._))
            .Returns<(byte[], string, bool, MediaTypeId)>(default);
        var sut = new RegistrationBusinessLogic(_options, null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        //Act
        Task Act() => sut.GetRegistrationDocumentAsync(documentId);

        // Assert
        var result = await Assert.ThrowsAsync<NotFoundException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_DOCUMENT_NOT_EXIST.ToString());
    }

    #region GetDocumentAsync

    [Fact]
    public async Task GetDocumentAsync_WithValidData_ReturnsExpected()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var content = new byte[7];
        A.CallTo(() => _documentRepository.GetDocumentIdWithCompanyUserCheckAsync(documentId, _identity.IdentityId))
            .Returns((documentId, true, true, false));
        A.CallTo(() => _documentRepository.GetDocumentByIdAsync(A<Guid>._, A<IEnumerable<DocumentTypeId>>._))
            .Returns(new Document(documentId, content, content, "test.pdf", MediaTypeId.PDF, DateTimeOffset.UtcNow, DocumentStatusId.LOCKED, DocumentTypeId.APP_CONTRACT, content.Length));
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        var result = await sut.GetDocumentContentAsync(documentId);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be("test.pdf");
        result.MediaType.Should().Be("application/pdf");
        A.CallTo(() => _documentRepository.GetDocumentByIdAsync(documentId, A<IEnumerable<DocumentTypeId>>.That.IsSameSequenceAs(new[] { DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT }))).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetDocumentAsync_WithoutDocument_ThrowsNotFoundException()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        A.CallTo(() => _documentRepository.GetDocumentIdWithCompanyUserCheckAsync(documentId, _identity.IdentityId))
            .Returns((Guid.Empty, false, false, false));
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.GetDocumentContentAsync(documentId);

        // Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_DOCUMENT_NOT_EXIST.ToString());
    }

    [Fact]
    public async Task GetDocumentAsync_WithWrongUser_ThrowsForbiddenException()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        A.CallTo(() => _documentRepository.GetDocumentIdWithCompanyUserCheckAsync(documentId, _identity.IdentityId))
            .Returns((documentId, false, false, false));
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.GetDocumentContentAsync(documentId);

        // Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_NOT_PERMITTED_DOCUMENT_ACCESS.ToString());
    }

    [Fact]
    public async Task GetDocumentAsync_WithConfirmedApplicationStatus_ThrowsForbiddenException()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        A.CallTo(() => _documentRepository.GetDocumentIdWithCompanyUserCheckAsync(documentId, _identity.IdentityId))
            .Returns((documentId, true, true, true));
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.GetDocumentContentAsync(documentId);

        // Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_DOCUMENT_ACCESSIBLE_AFTER_ONBOARDING_PROCESS.ToString());
    }

    #endregion

    #region SetInvitationStatus

    [Fact]
    public async Task SetInvitationStatusAsync_ReturnsExpected()
    {
        // Arrange
        var now = DateTimeOffset.Now;
        var invitation = _fixture.Build<Invitation>()
            .With(x => x.InvitationStatusId, InvitationStatusId.PENDING)
            .Create();
        var application = _fixture.Build<CompanyApplication>()
            .With(x => x.Id, _existingApplicationId)
            .Create();
        var retval = _fixture.Create<int>();

        A.CallTo(() => _dateTimeProvider.OffsetNow).Returns(now);
        A.CallTo(() => _invitationRepository.GetInvitationStatusAsync(_identity.IdentityId))
            .Returns(invitation);
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(A<Guid>._, A<Action<CompanyApplication>>._))
            .Invokes((Guid _, Action<CompanyApplication> setOptionalFields) =>
            {
                setOptionalFields.Invoke(application);
            });
        A.CallTo(() => _portalRepositories.SaveAsync()).Returns(retval);

        var sut = new RegistrationBusinessLogic(_options, null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        var result = await sut.SetInvitationStatusAsync();

        // Assert
        result.Should().Be(retval);
        A.CallTo(() => _invitationRepository.GetInvitationStatusAsync(_identity.IdentityId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(A<Guid>._, A<Action<CompanyApplication>>._)).MustHaveHappenedOnceExactly();
        invitation.InvitationStatusId.Should().Be(InvitationStatusId.ACCEPTED);
        application.DateLastChanged.Should().Be(now);
    }

    [Fact]
    public async Task SetInvitationStatusAsync_Throws_ForbiddenException()
    {
        // Arrange
        A.CallTo(() => _invitationRepository.GetInvitationStatusAsync(A<Guid>._))
            .Returns<Invitation?>(null);
        var sut = new RegistrationBusinessLogic(_options, null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        //Act
        Task Act() => sut.SetInvitationStatusAsync();

        // Assert
        var result = await Assert.ThrowsAsync<ForbiddenException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_USER_NOT_ASSOCIATED_INVITATION.ToString());
    }

    #endregion

    #region DeleteRegistrationDocument

    [Fact]
    public async Task DeleteRegistrationDocumentAsync_ReturnsExpected()
    {
        // Arrange
        var now = DateTimeOffset.Now;
        var documentId = _fixture.Create<Guid>();
        var applicationIds = new[] {
            _fixture.Create<Guid>(),
            _fixture.Create<Guid>()
        };
        var modifiedApplication = new List<(CompanyApplication Initial, CompanyApplication Modified)>();
        var settings = new RegistrationSettings
        {
            ApplicationStatusIds = [
                CompanyApplicationStatusId.CONFIRMED,
                CompanyApplicationStatusId.SUBMITTED,
                CompanyApplicationStatusId.DECLINED
            ],
            DocumentTypeIds = [
                DocumentTypeId.CX_FRAME_CONTRACT,
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        A.CallTo(() => _dateTimeProvider.OffsetNow).Returns(now);

        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplications(A<IEnumerable<(Guid applicationId, Action<CompanyApplication>?, Action<CompanyApplication>)>>._))
            .Invokes((IEnumerable<(Guid CompanyApplicationId, Action<CompanyApplication>? Initialize, Action<CompanyApplication> Modify)> companyApplicationKeyActions) =>
            {
                foreach (var x in companyApplicationKeyActions)
                {
                    var initial = new CompanyApplication(x.CompanyApplicationId, Guid.Empty, default, default, default);
                    x.Initialize?.Invoke(initial);
                    var modified = new CompanyApplication(x.CompanyApplicationId, Guid.Empty, default, default, default);
                    x.Modify(modified);
                    modifiedApplication.Add((initial, modified));
                }
            });
        A.CallTo(() => _documentRepository.GetDocumentDetailsForApplicationUntrackedAsync(documentId, _identity.CompanyId, settings.ApplicationStatusIds))
            .Returns((documentId, DocumentStatusId.PENDING, true, DocumentTypeId.CX_FRAME_CONTRACT, false, applicationIds));

        var sut = new RegistrationBusinessLogic(Options.Create(settings), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        var result = await sut.DeleteRegistrationDocumentAsync(documentId);

        // Assert
        A.CallTo(() => _documentRepository.GetDocumentDetailsForApplicationUntrackedAsync(documentId, _identity.CompanyId, settings.ApplicationStatusIds)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _documentRepository.RemoveDocument(documentId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplications(A<IEnumerable<(Guid companyApplicationId, Action<CompanyApplication>?, Action<CompanyApplication>)>>.That.Matches(x => x.Count() == 2))).MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
        result.Should().BeTrue();
        modifiedApplication.Should().HaveCount(2).And.Satisfy(
            x => x.Modified.DateLastChanged == now && x.Initial.Id == applicationIds[0],
            x => x.Modified.DateLastChanged == now && x.Initial.Id == applicationIds[1]
        );
    }

    [Fact]
    public async Task DeleteRegistrationDocumentAsync_DocumentTypeId_ConflictException()
    {
        // Arrange
        var documentId = _fixture.Create<Guid>();
        var applicationId = _fixture.CreateMany<Guid>();
        var settings = new RegistrationSettings
        {
            ApplicationStatusIds = [
                CompanyApplicationStatusId.CONFIRMED,
                CompanyApplicationStatusId.SUBMITTED,
                CompanyApplicationStatusId.DECLINED
            ],
            DocumentTypeIds = [
                DocumentTypeId.CX_FRAME_CONTRACT,
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        A.CallTo(() => _documentRepository.GetDocumentDetailsForApplicationUntrackedAsync(documentId, _identity.CompanyId, settings.ApplicationStatusIds))
            .Returns((documentId, DocumentStatusId.PENDING, true, DocumentTypeId.CONFORMITY_APPROVAL_BUSINESS_APPS, false, applicationId));

        var sut = new RegistrationBusinessLogic(Options.Create(settings), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.DeleteRegistrationDocumentAsync(documentId);

        // Assert
        var result = await Assert.ThrowsAsync<ConflictException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_CONFLICT_DOCUMENT_DELETION_NOT_ALLOWED.ToString());
    }

    [Fact]
    public async Task DeleteRegistrationDocumentAsync_Throws_NotFoundException()
    {
        // Arrange;
        A.CallTo(() => _documentRepository.GetDocumentDetailsForApplicationUntrackedAsync(A<Guid>._, _identity.CompanyId, A<IEnumerable<CompanyApplicationStatusId>>._))
            .Returns<(Guid, DocumentStatusId, bool, DocumentTypeId, bool, IEnumerable<Guid>)>(default);

        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.DeleteRegistrationDocumentAsync(_fixture.Create<Guid>());

        // Assert
        var result = await Assert.ThrowsAsync<NotFoundException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_DOCUMENT_NOT_EXIST.ToString());
    }

    [Fact]
    public async Task DeleteRegistrationDocumentAsync_Throws_ConflictException()
    {
        // Arrange;
        var documentId = _fixture.Create<Guid>();
        var applicationId = _fixture.CreateMany<Guid>();
        var settings = new RegistrationSettings
        {
            ApplicationStatusIds = [
                CompanyApplicationStatusId.CONFIRMED,
                CompanyApplicationStatusId.SUBMITTED,
                CompanyApplicationStatusId.DECLINED
            ],
            DocumentTypeIds = [
                DocumentTypeId.CX_FRAME_CONTRACT,
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        A.CallTo(() => _documentRepository.GetDocumentDetailsForApplicationUntrackedAsync(A<Guid>._, _identity.CompanyId, A<IEnumerable<CompanyApplicationStatusId>>._))
            .Returns((documentId, DocumentStatusId.PENDING, true, DocumentTypeId.CX_FRAME_CONTRACT, true, applicationId));

        var sut = new RegistrationBusinessLogic(Options.Create(settings), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.DeleteRegistrationDocumentAsync(documentId);

        // Assert
        var result = await Assert.ThrowsAsync<ConflictException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_CONFLICT_DOCUMENT_DELETION_NOT_ALLOWED_APP_CLOSED.ToString());
    }

    [Fact]
    public async Task DeleteRegistrationDocumentAsync_Throws_ForbiddenException()
    {
        // Arrange;
        var documentId = _fixture.Create<Guid>();
        var applicationId = _fixture.CreateMany<Guid>();
        var settings = new RegistrationSettings
        {
            ApplicationStatusIds = [
                CompanyApplicationStatusId.CONFIRMED,
                CompanyApplicationStatusId.SUBMITTED,
                CompanyApplicationStatusId.DECLINED
            ],
            DocumentTypeIds = [
                DocumentTypeId.CX_FRAME_CONTRACT,
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        A.CallTo(() => _documentRepository.GetDocumentDetailsForApplicationUntrackedAsync(A<Guid>._, _identity.CompanyId, A<IEnumerable<CompanyApplicationStatusId>>._))
            .Returns((documentId, DocumentStatusId.PENDING, false, DocumentTypeId.CX_FRAME_CONTRACT, false, applicationId));

        var sut = new RegistrationBusinessLogic(Options.Create(settings), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.DeleteRegistrationDocumentAsync(documentId);

        // Assert
        var result = await Assert.ThrowsAsync<ForbiddenException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_FORBIDDEN_USER_NOT_ALLOWED_DELETE_DOCUMENT.ToString());
    }

    [Fact]
    public async Task DeleteRegistrationDocumentAsync_DocumentStatusId_Throws_ConflictException()
    {
        // Arrange;
        var documentId = _fixture.Create<Guid>();
        var applicationId = _fixture.CreateMany<Guid>();
        var settings = new RegistrationSettings
        {
            ApplicationStatusIds = [
                CompanyApplicationStatusId.CONFIRMED,
                CompanyApplicationStatusId.SUBMITTED,
                CompanyApplicationStatusId.DECLINED
            ],
            DocumentTypeIds = [
                DocumentTypeId.CX_FRAME_CONTRACT,
                DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT
            ]
        };
        A.CallTo(() => _documentRepository.GetDocumentDetailsForApplicationUntrackedAsync(A<Guid>._, _identity.CompanyId, A<IEnumerable<CompanyApplicationStatusId>>._))
            .Returns((documentId, DocumentStatusId.LOCKED, true, DocumentTypeId.CX_FRAME_CONTRACT, false, applicationId));

        var sut = new RegistrationBusinessLogic(Options.Create(settings), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.DeleteRegistrationDocumentAsync(documentId);

        // Assert
        var result = await Assert.ThrowsAsync<ConflictException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_CONFLICT_DELETION_NOT_ALLOWED_LOCKED.ToString());
    }

    [Fact]
    public async Task DeleteRegistrationDocumentAsync_Throws_ControllerArgumentException()
    {
        // Arrange;
        var sut = new RegistrationBusinessLogic(Options.Create(new RegistrationSettings()), null!, null!, null!, null!, _portalRepositories, null!, _identityService, _dateTimeProvider, _mailingProcessCreation);

        // Act
        Task Act() => sut.DeleteRegistrationDocumentAsync(default);

        // Assert
        var result = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_ARG_DOCUMENT_ID_NOT_EMPTY.ToString());
    }

    #endregion

    #region Setup  

    private void SetupRepositories()
    {
        var invitedUser = _fixture.CreateMany<(InvitationStatusId InvitationStatus, string? EmailId, IEnumerable<string> Roles)>(1).ToAsyncEnumerable();

        A.CallTo(() => _invitationRepository.GetInvitedUserDetailsUntrackedAsync(_existingApplicationId))
            .Returns(invitedUser);
        A.CallTo(() => _invitationRepository.GetInvitedUserDetailsUntrackedAsync(Guid.Empty)).Throws(new Exception());

        A.CallTo(() => _applicationRepository.IsValidApplicationForCompany(_existingApplicationId, _identity.CompanyId))
            .Returns(true);
        A.CallTo(() => _applicationRepository.IsValidApplicationForCompany(_existingApplicationId, A<Guid>.That.Not.Matches(x => x == _identity.CompanyId)))
            .Returns(false);
        A.CallTo(() => _applicationRepository.IsValidApplicationForCompany(
                A<Guid>.That.Not.Matches(x => x == _existingApplicationId), _identity.CompanyId))
            .Returns(false);
        A.CallTo(() => _applicationRepository.IsValidApplicationForCompany(
                A<Guid>.That.Not.Matches(x => x == _existingApplicationId), A<Guid>.That.Not.Matches(x => x == _identity.CompanyId)))
            .Returns(false);

        A.CallTo(() => _countryRepository.GetCountryAssignedIdentifiers(A<string>._, A<IEnumerable<UniqueIdentifierId>>._))
            .ReturnsLazily((string alpha2Code, IEnumerable<UniqueIdentifierId> identifiers) => (true, identifiers));

        A.CallTo(() => _companyRepository.CreateAddress(A<string>._, A<string>._, A<string>._, A<string>._, A<Action<Address>>._))
            .ReturnsLazily((string city, string streetName, string region, string alpha2Code, Action<Address>? setParameters) =>
            {
                var address = new Address(Guid.NewGuid(), city, streetName, region, alpha2Code, _fixture.Create<DateTimeOffset>());
                setParameters?.Invoke(address);
                return address;
            });

        A.CallTo(() => _countryRepository.CheckCountryExistsByAlpha2CodeAsync(_alpha2code))
            .Returns(true);
        A.CallTo(() => _countryRepository.CheckCountryExistsByAlpha2CodeAsync(A<string>.That.Not.Matches(c => c == _alpha2code)))
            .Returns(true);

        A.CallTo(() => _portalRepositories.GetInstance<IDocumentRepository>())
            .Returns(_documentRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IInvitationRepository>())
            .Returns(_invitationRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IUserRepository>())
            .Returns(_userRepository);
        A.CallTo(() => _portalRepositories.GetInstance<ICompanyRepository>())
            .Returns(_companyRepository);
        A.CallTo(() => _portalRepositories.GetInstance<ICompanyRolesRepository>())
            .Returns(_companyRolesRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IApplicationRepository>())
            .Returns(_applicationRepository);
        A.CallTo(() => _portalRepositories.GetInstance<ICountryRepository>())
            .Returns(_countryRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IConsentRepository>())
            .Returns(_consentRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IStaticDataRepository>())
            .Returns(_staticDataRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IPortalProcessStepRepository>())
            .Returns(_processStepRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IIdentityProviderRepository>())
            .Returns(_identityProviderRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IUserRolesRepository>())
            .Returns(_userRoleRepository);
    }

    private void SetupFakesForInvitation()
    {
        A.CallTo(() => _userProvisioningService.CreateOwnCompanyIdpUsersAsync(A<CompanyNameIdpAliasData>._, A<IAsyncEnumerable<UserCreationRoleDataIdpInfo>>._, A<Action<UserCreationCallbackData>>._, A<CancellationToken>._))
            .ReturnsLazily((CompanyNameIdpAliasData _, IAsyncEnumerable<UserCreationRoleDataIdpInfo> userCreationInfos, Action<UserCreationCallbackData>? onSuccess, CancellationToken _) =>
                userCreationInfos.Select(userCreationInfo => _processLine(userCreationInfo)));

        A.CallTo(() => _userProvisioningService.GetRoleDatas(A<IEnumerable<UserRoleConfig>>._))
            .ReturnsLazily((IEnumerable<UserRoleConfig> clientRoles) => clientRoles.SelectMany(r => r.UserRoleNames.Select(role => _fixture.Build<UserRoleData>().With(x => x.UserRoleText, role).Create())).ToAsyncEnumerable());

        A.CallTo(() => _identityProviderProvisioningService.GetIdentityProviderDisplayName(A<string>._)).Returns(_displayName);

        A.CallTo(() => _userProvisioningService.GetCompanyNameSharedIdpAliasData(A<Guid>._, A<Guid?>._)).Returns(
            (
                _fixture.Create<CompanyNameIdpAliasData>(),
                _fixture.Create<string>()
            ));

        A.CallTo(() => _processLine(A<UserCreationRoleDataIdpInfo>._)).ReturnsLazily(
            (UserCreationRoleDataIdpInfo creationInfo) => _fixture.Build<(Guid CompanyUserId, string UserName, string? Password, Exception? Error)>()
                .With(x => x.UserName, creationInfo.UserName)
                .With(x => x.Error, default(Exception?))
                .Create());
    }

    #endregion

    #region GetApplicationsDeclineData

    [Fact]
    public async Task GetApplicationsDeclineData_CallsExpected()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var data = ("Acme Corp", "Tony", "Stark", "t.stark@acme.corp", new (Guid, CompanyApplicationStatusId, IEnumerable<(string?, string?, string?)>)[] {
            (applicationId, CompanyApplicationStatusId.CREATED, new(string?, string?, string?)[] {
                ("Test", "User", "t.user@acme.corp"),
                (null, null, "foo@bar.org")
            }.AsEnumerable())
        });

        A.CallTo(() => _applicationRepository.GetCompanyApplicationsDeclineData(A<Guid>._, A<IEnumerable<CompanyApplicationStatusId>>._))
            .Returns(data);

        var options = Options.Create(new RegistrationSettings
        {
            ApplicationDeclineStatusIds = [CompanyApplicationStatusId.CREATED]
        });

        var sut = new RegistrationBusinessLogic(options, null!, null!, null!, null!, _portalRepositories, null!, _identityService, null!, _mailingProcessCreation);

        // Act
        var result = await sut.GetApplicationsDeclineData();

        // Assert
        A.CallTo(() => _applicationRepository.GetCompanyApplicationsDeclineData(_identity.IdentityId, A<IEnumerable<CompanyApplicationStatusId>>.That.IsSameSequenceAs(new[] { CompanyApplicationStatusId.CREATED })))
            .MustHaveHappenedOnceExactly();

        result.Should().ContainSingle().Which.Should().Match<CompanyApplicationDeclineData>(x =>
            x.ApplicationId == applicationId &&
            x.ApplicationStatus == CompanyApplicationStatusId.CREATED &&
            x.CompanyName == "Acme Corp" &&
            x.User == "Tony, Stark (t.stark@acme.corp)" &&
            x.Users.SequenceEqual(new[] { "Test, User (t.user@acme.corp)", "foo@bar.org" }));
    }

    #endregion

    #region DeclineApplicationRegistration

    [Fact]
    public async Task DeclineApplicationRegistrationAsync_CallsExpected()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var idpStatusData = new IdentityProviderStatusData[] {
            new(Guid.NewGuid(), IdentityProviderTypeId.SHARED),
            new(Guid.NewGuid(), IdentityProviderTypeId.OWN),
            new(Guid.NewGuid(), IdentityProviderTypeId.MANAGED),
        };
        var invitationStatusData = new InvitationsStatusData[] {
            new(Guid.NewGuid(), InvitationStatusId.ACCEPTED),
            new(Guid.NewGuid(), InvitationStatusId.CREATED),
            new(Guid.NewGuid(), InvitationStatusId.PENDING),
        };
        var identityStatusData = new CompanyUserStatusData[] {
            new(Guid.NewGuid(), null, null, "email1", UserStatusId.ACTIVE, _fixture.CreateMany<Guid>(3).ToImmutableArray()),
            new(Guid.NewGuid(), "First", "Last", "email2", UserStatusId.INACTIVE, _fixture.CreateMany<Guid>(3).ToImmutableArray()),
            new(Guid.NewGuid(), "Other", null, "email3", UserStatusId.PENDING, _fixture.CreateMany<Guid>(3).ToImmutableArray())
        };
        var documentStatusData = new DocumentStatusData[] {
            new(Guid.NewGuid(), DocumentStatusId.PENDING),
            new(Guid.NewGuid(), DocumentStatusId.LOCKED)
        };
        var applicationDeclineData = new ApplicationDeclineData(
            idpStatusData,
            "TestCompany",
            CompanyApplicationStatusId.CREATED,
            invitationStatusData,
            identityStatusData,
            documentStatusData
        );
        var options = Options.Create(new RegistrationSettings
        {
            ApplicationDeclineStatusIds = [CompanyApplicationStatusId.CREATED]
        });

        A.CallTo(() => _applicationRepository.GetDeclineApplicationDataForApplicationId(A<Guid>._, A<Guid>._, A<IEnumerable<CompanyApplicationStatusId>>._))
            .Returns((true, true, applicationDeclineData));

        CompanyApplication? application = null;
        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(applicationId, A<Action<CompanyApplication>>._))
            .Invokes((Guid _, Action<CompanyApplication> setOptionalFields) =>
            {
                application = new(applicationId, Guid.Empty, default, default, default);
                setOptionalFields.Invoke(application);
            });

        Company? company = null;
        A.CallTo(() => _companyRepository.AttachAndModifyCompany(A<Guid>._, A<Action<Company>>._, A<Action<Company>>._))
            .Invokes((Guid companyId, Action<Company>? initialize, Action<Company> modify) =>
            {
                company = new Company(companyId, null!, default, default);
                initialize?.Invoke(company);
                modify(company);
            });

        var modifiedInvitationsBuilder = ImmutableArray.CreateBuilder<(Invitation Initial, Invitation Modified)>();
        A.CallTo(() => _invitationRepository.AttachAndModifyInvitations(A<IEnumerable<(Guid, Action<Invitation>?, Action<Invitation>)>>._))
            .Invokes((IEnumerable<(Guid InvitationId, Action<Invitation>? Initialize, Action<Invitation> Modify)> invitationKeyActions) =>
            {
                foreach (var x in invitationKeyActions)
                {
                    var initial = new Invitation(x.InvitationId, Guid.Empty, Guid.Empty, default, default);
                    x.Initialize?.Invoke(initial);
                    var modified = new Invitation(x.InvitationId, Guid.Empty, Guid.Empty, default, default);
                    x.Modify(modified);
                    modifiedInvitationsBuilder.Add((initial, modified));
                }
            });

        var modifiedDocumentsBuilder = ImmutableArray.CreateBuilder<(Document Initial, Document Modified)>();
        A.CallTo(() => _documentRepository.AttachAndModifyDocuments(A<IEnumerable<(Guid DocumentId, Action<Document>?, Action<Document>)>>._))
            .Invokes((IEnumerable<(Guid DocumentId, Action<Document>? Initialize, Action<Document> Modify)> documentKeyActions) =>
            {
                foreach (var x in documentKeyActions)
                {
                    var initial = new Document(x.DocumentId, null!, null!, null!, default, default, default, default, default);
                    x.Initialize?.Invoke(initial);
                    var modified = new Document(x.DocumentId, null!, null!, null!, default, default, default, default, default);
                    x.Modify(modified);
                    modifiedDocumentsBuilder.Add((initial, modified));
                }
            });

        var createdProcessesBuilder = ImmutableArray.CreateBuilder<Process>();
        A.CallTo(() => _processStepRepository.CreateProcessRange(A<IEnumerable<ProcessTypeId>>._))
            .ReturnsLazily((IEnumerable<ProcessTypeId> processTypeIds) =>
            {
                var processes = processTypeIds.Select(x => new Process(Guid.NewGuid(), x, Guid.NewGuid())).ToImmutableArray();
                createdProcessesBuilder.AddRange(processes);
                return processes;
            });

        A.CallTo(() => _processStepRepository.CreateProcessStepRange(A<IEnumerable<(ProcessStepTypeId, ProcessStepStatusId, Guid)>>._))
            .ReturnsLazily((IEnumerable<(ProcessStepTypeId ProcessStepTypeId, ProcessStepStatusId ProcessStepStatusId, Guid ProcessId)> processStepStatusTypeIds) =>
                processStepStatusTypeIds.Select(x => new ProcessStep<Process, ProcessTypeId, ProcessStepTypeId>(Guid.NewGuid(), x.ProcessStepTypeId, x.ProcessStepStatusId, x.ProcessId, DateTimeOffset.UtcNow)).ToImmutableArray());

        var modifiedIdentitiesBuilder = ImmutableArray.CreateBuilder<(Identity Initial, Identity Modified)>();
        A.CallTo(() => _userRepository.AttachAndModifyIdentities(A<IEnumerable<(Guid IdentityId, Action<Identity>?, Action<Identity>)>>._))
            .Invokes((IEnumerable<(Guid IdentityId, Action<Identity>? Initialize, Action<Identity> Modify)> identityKeyActions) =>
            {
                foreach (var x in identityKeyActions)
                {
                    var initial = new Identity(x.IdentityId, default, Guid.Empty, default, default);
                    x.Initialize?.Invoke(initial);
                    var modified = new Identity(x.IdentityId, default, Guid.Empty, default, default);
                    x.Modify(modified);
                    modifiedIdentitiesBuilder.Add((initial, modified));
                }
            });

        var sut = new RegistrationBusinessLogic(options, null!, null!, null!, null!, _portalRepositories, null!, _identityService, null!, _mailingProcessCreation);

        // Act
        await sut.DeclineApplicationRegistrationAsync(applicationId);

        var modifiedInvitations = modifiedInvitationsBuilder.ToImmutable();
        var modifiedDocuments = modifiedDocumentsBuilder.ToImmutable();
        var createdProcesses = createdProcessesBuilder.ToImmutable();
        var modifiedIdentities = modifiedIdentitiesBuilder.ToImmutable();

        // Assert

        A.CallTo(() => _applicationRepository
            .GetDeclineApplicationDataForApplicationId(applicationId, _identity.CompanyId, A<IEnumerable<CompanyApplicationStatusId>>.That.IsSameSequenceAs(new[]
                {
                    CompanyApplicationStatusId.CREATED
                })))
            .MustHaveHappenedOnceExactly();

        // DeclineApplication

        A.CallTo(() => _applicationRepository.AttachAndModifyCompanyApplication(applicationId, A<Action<CompanyApplication>>._))
            .MustHaveHappenedOnceExactly();
        application.Should().NotBeNull();
        application!.ApplicationStatusId.Should().Be(CompanyApplicationStatusId.DECLINED);

        // DeleteCompany

        A.CallTo(() => _companyRepository.AttachAndModifyCompany(_identity.CompanyId, A<Action<Company>>._, A<Action<Company>>._))
            .MustHaveHappenedOnceExactly();
        company.Should().NotBeNull().And.Match<Company>(x =>
            x.Id == _identity.CompanyId &&
            x.CompanyStatusId == CompanyStatusId.DELETED
        );

        // DeclineInvitations

        A.CallTo(() => _invitationRepository
            .AttachAndModifyInvitations(A<IEnumerable<(Guid InvitationId, Action<Invitation>?, Action<Invitation>)>>.That.Matches(x => x.Select(y => y.InvitationId).SequenceEqual(new Guid[]
                {
                    invitationStatusData[0].InvitationId,
                    invitationStatusData[1].InvitationId,
                    invitationStatusData[2].InvitationId
                }))))
            .MustHaveHappenedOnceExactly();

        modifiedInvitations.Should().HaveCount(3)
            .And.AllSatisfy(x => x.Modified.InvitationStatusId.Should().Be(InvitationStatusId.DECLINED))
            .And.Satisfy(
                x => x.Initial.Id == invitationStatusData[0].InvitationId && x.Initial.InvitationStatusId == invitationStatusData[0].InvitationStatusId,
                x => x.Initial.Id == invitationStatusData[1].InvitationId && x.Initial.InvitationStatusId == invitationStatusData[1].InvitationStatusId,
                x => x.Initial.Id == invitationStatusData[2].InvitationId && x.Initial.InvitationStatusId == invitationStatusData[2].InvitationStatusId
            );

        // DeactivateDocuments

        _ = A.CallTo(() => _documentRepository
            .AttachAndModifyDocuments(A<IEnumerable<(Guid DocumentId, Action<Document>?, Action<Document>)>>.That.Matches(x => x.Select(y => y.DocumentId).SequenceEqual(new Guid[]
                {
                    documentStatusData[0].DocumentId,
                    documentStatusData[1].DocumentId,
                }))))
            .MustHaveHappenedOnceExactly();

        modifiedDocuments.Should().HaveCount(2)
            .And.AllSatisfy(x => x.Modified.DocumentStatusId.Should().Be(DocumentStatusId.INACTIVE))
            .And.Satisfy(
                x => x.Initial.Id == documentStatusData[0].DocumentId && x.Initial.DocumentStatusId == documentStatusData[0].StatusId,
                x => x.Initial.Id == documentStatusData[1].DocumentId && x.Initial.DocumentStatusId == documentStatusData[1].StatusId
            );

        // ScheduleDeleteIdentityProviders

        A.CallTo(() => _identityProviderRepository
            .DeleteCompanyIdentityProviderRange(A<IEnumerable<(Guid CompanyId, Guid IdentityProviderId)>>.That.IsSameSequenceAs(new (Guid, Guid)[]
                {
                    new(_identity.CompanyId, idpStatusData[2].IdentityProviderId)
                })))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _processStepRepository
            .CreateProcessRange(A<IEnumerable<ProcessTypeId>>.That.IsSameSequenceAs(Enumerable.Repeat(ProcessTypeId.IDENTITYPROVIDER_PROVISIONING, 2))))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _processStepRepository
            .CreateProcessStepRange(A<IEnumerable<(ProcessStepTypeId, ProcessStepStatusId, Guid)>>.That.IsSameSequenceAs(new (ProcessStepTypeId, ProcessStepStatusId, Guid)[]
                {
                    new(ProcessStepTypeId.DELETE_IDP_SHARED_REALM, ProcessStepStatusId.TODO, createdProcesses[0].Id),
                    new(ProcessStepTypeId.DELETE_CENTRAL_IDENTITY_PROVIDER, ProcessStepStatusId.TODO, createdProcesses[1].Id)
                })))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _identityProviderRepository
            .CreateIdentityProviderAssignedProcessRange(A<IEnumerable<(Guid IdentityProviderId, Guid ProcessId)>>.That.IsSameSequenceAs(new (Guid, Guid)[]
                {
                    new(idpStatusData[0].IdentityProviderId, createdProcesses[0].Id),
                    new(idpStatusData[1].IdentityProviderId, createdProcesses[1].Id)
                })))
            .MustHaveHappenedOnceExactly();

        // ScheduleDeleteCompanyUsers

        A.CallTo(() => _userRepository.AttachAndModifyIdentities(A<IEnumerable<(Guid IdentityId, Action<Identity>?, Action<Identity>)>>.That.Matches(x => x.Select(y => y.IdentityId).SequenceEqual(new Guid[]
                {
                    identityStatusData[0].CompanyUserId,
                    identityStatusData[1].CompanyUserId,
                    identityStatusData[2].CompanyUserId,
                }))))
            .MustHaveHappenedOnceExactly();

        modifiedIdentities.Should().HaveCount(3)
            .And.AllSatisfy(x => x.Modified.UserStatusId.Should().Be(UserStatusId.DELETED))
            .And.Satisfy(
                x => x.Initial.Id == identityStatusData[0].CompanyUserId,
                x => x.Initial.Id == identityStatusData[1].CompanyUserId,
                x => x.Initial.Id == identityStatusData[2].CompanyUserId
            );

        A.CallTo(() => _userRoleRepository.DeleteCompanyUserAssignedRoles(A<IEnumerable<(Guid CompanyUserId, Guid RoleId)>>.That.IsSameSequenceAs(new (Guid, Guid)[]
                {
                    new (identityStatusData[0].CompanyUserId, identityStatusData[0].IdentityAssignedRoleIds.ElementAt(0)),
                    new (identityStatusData[0].CompanyUserId, identityStatusData[0].IdentityAssignedRoleIds.ElementAt(1)),
                    new (identityStatusData[0].CompanyUserId, identityStatusData[0].IdentityAssignedRoleIds.ElementAt(2)),
                    new (identityStatusData[1].CompanyUserId, identityStatusData[1].IdentityAssignedRoleIds.ElementAt(0)),
                    new (identityStatusData[1].CompanyUserId, identityStatusData[1].IdentityAssignedRoleIds.ElementAt(1)),
                    new (identityStatusData[1].CompanyUserId, identityStatusData[1].IdentityAssignedRoleIds.ElementAt(2)),
                    new (identityStatusData[2].CompanyUserId, identityStatusData[2].IdentityAssignedRoleIds.ElementAt(0)),
                    new (identityStatusData[2].CompanyUserId, identityStatusData[2].IdentityAssignedRoleIds.ElementAt(1)),
                    new (identityStatusData[2].CompanyUserId, identityStatusData[2].IdentityAssignedRoleIds.ElementAt(2)),
                })))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _processStepRepository
            .CreateProcessRange(A<IEnumerable<ProcessTypeId>>.That.IsSameSequenceAs(Enumerable.Repeat(ProcessTypeId.USER_PROVISIONING, 3))))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _processStepRepository
            .CreateProcessStepRange(A<IEnumerable<(ProcessStepTypeId, ProcessStepStatusId, Guid)>>.That.IsSameSequenceAs(new (ProcessStepTypeId, ProcessStepStatusId, Guid)[]
                {
                    new(ProcessStepTypeId.DELETE_CENTRAL_USER, ProcessStepStatusId.TODO, createdProcesses[2].Id),
                    new(ProcessStepTypeId.DELETE_CENTRAL_USER, ProcessStepStatusId.TODO, createdProcesses[3].Id),
                    new(ProcessStepTypeId.DELETE_CENTRAL_USER, ProcessStepStatusId.TODO, createdProcesses[4].Id)
                })))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _userRepository.CreateCompanyUserAssignedProcessRange(A<IEnumerable<(Guid CompanyUserId, Guid ProcessId)>>.That.IsSameSequenceAs(new (Guid, Guid)[]
                {
                    new(identityStatusData[0].CompanyUserId, createdProcesses[2].Id),
                    new(identityStatusData[1].CompanyUserId, createdProcesses[3].Id),
                    new(identityStatusData[2].CompanyUserId, createdProcesses[4].Id),
                })))
            .MustHaveHappenedOnceExactly();

        // CreateDeclineApplicationEmailProcesses
        A.CallTo(() => _mailingProcessCreation.CreateMailProcess(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>>._))
            .MustNotHaveHappened();

        // final save
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DeclineApplicationRegistrationAsync_ThrowsNotFoundException_ReturnsExpected()
    {
        // Arrange
        var applicationId = _fixture.Create<Guid>();
        var options = Options.Create(new RegistrationSettings
        {
            ApplicationDeclineStatusIds = [CompanyApplicationStatusId.CREATED]
        });

        A.CallTo(() => _applicationRepository.GetDeclineApplicationDataForApplicationId(A<Guid>._, A<Guid>._, A<IEnumerable<CompanyApplicationStatusId>>._))
            .Returns<(bool, bool, ApplicationDeclineData?)>(default);

        var sut = new RegistrationBusinessLogic(options, null!, null!, null!, null!, _portalRepositories, null!, _identityService, null!, _mailingProcessCreation);

        // Act
        Task Act() => sut.DeclineApplicationRegistrationAsync(applicationId);

        // Assert
        var result = await Assert.ThrowsAsync<NotFoundException>(Act);
        result.Message.Should().Be(RegistrationErrors.REGISTRATION_APPLICATION_NOT_EXIST.ToString());
    }

    #endregion
}
