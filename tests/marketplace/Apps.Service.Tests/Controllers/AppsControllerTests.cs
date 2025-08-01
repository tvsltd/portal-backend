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
using Microsoft.AspNetCore.Mvc;
using Org.Eclipse.TractusX.Portal.Backend.Apps.Service.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Apps.Service.ViewModels;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Identity;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Models;
using Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Tests.Shared;
using Org.Eclipse.TractusX.Portal.Backend.Tests.Shared.Extensions;
using System.Collections.Immutable;
using Xunit;

namespace Org.Eclipse.TractusX.Portal.Backend.Apps.Service.Controllers.Tests;

public class AppsControllerTests
{
    private readonly string _accessToken = "THISISTHEACCESSTOKEN";
    private readonly IFixture _fixture;
    private readonly IAppsBusinessLogic _logic;
    private readonly AppsController _controller;
    private readonly IIdentityData _identity;

    public AppsControllerTests()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        _identity = A.Fake<IIdentityData>();
        A.CallTo(() => _identity.IdentityId).Returns(Guid.NewGuid());
        A.CallTo(() => _identity.IdentityTypeId).Returns(IdentityTypeId.COMPANY_USER);
        A.CallTo(() => _identity.CompanyId).Returns(Guid.NewGuid());
        _logic = A.Fake<IAppsBusinessLogic>();
        _controller = new AppsController(_logic);
        _controller.AddControllerContextWithClaimAndBearer(_accessToken, _identity);
    }

    [Fact]
    public async Task GetAllActiveAppsAsync_ReturnsExpectedCount()
    {
        //Arrange
        var data = new AsyncEnumerableStub<AppData>(_fixture.CreateMany<AppData>(5));
        A.CallTo(() => _logic.GetAllActiveAppsAsync(A<string?>._))
            .Returns(data.AsAsyncEnumerable());

        //Act
        var result = await _controller.GetAllActiveAppsAsync().ToListAsync();

        //Assert
        A.CallTo(() => _logic.GetAllActiveAppsAsync(null)).MustHaveHappenedOnceExactly();
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetAllBusinessAppsForCurrentUserAsync_ReturnsExpectedCount()
    {
        //Arrange
        var data = new AsyncEnumerableStub<BusinessAppData>(_fixture.CreateMany<BusinessAppData>(5));
        A.CallTo(() => _logic.GetAllUserUserBusinessAppsAsync())
            .Returns(data.AsAsyncEnumerable());

        //Act
        var result = await _controller.GetAllBusinessAppsForCurrentUserAsync().ToListAsync();

        //Assert
        A.CallTo(() => _logic.GetAllUserUserBusinessAppsAsync()).MustHaveHappenedOnceExactly();
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetAppDetailsByIdAsync_ReturnsExpectedCount()
    {
        //Arrange
        var appId = _fixture.Create<Guid>();
        var data = _fixture.Create<AppDetailResponse>();
        A.CallTo(() => _logic.GetAppDetailsByIdAsync(A<Guid>._, A<string?>._))
            .Returns(data);

        //Act
        var result = await _controller.GetAppDetailsByIdAsync(appId);

        //Assert
        A.CallTo(() => _logic.GetAppDetailsByIdAsync(appId, null)).MustHaveHappenedOnceExactly();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllFavouriteAppsForCurrentUser_ReturnsExpectedCount()
    {
        //Arrange
        var ids = new AsyncEnumerableStub<Guid>(_fixture.CreateMany<Guid>(5));
        A.CallTo(() => _logic.GetAllFavouriteAppsForUserAsync())
            .Returns(ids.AsAsyncEnumerable());

        //Act
        var result = await _controller.GetAllFavouriteAppsForCurrentUserAsync().ToListAsync();

        //Assert
        A.CallTo(() => _logic.GetAllFavouriteAppsForUserAsync()).MustHaveHappenedOnceExactly();
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task AddFavouriteAppForCurrentUserAsync_ReturnsBusinessLogic()
    {
        //Arrange
        var id = _fixture.Create<Guid>();

        //Act
        var result = await _controller.AddFavouriteAppForCurrentUserAsync(id);

        //Assert
        A.CallTo(() => _logic.AddFavouriteAppForUserAsync(id)).MustHaveHappenedOnceExactly();
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RemoveFavouriteAppForCurrentUserAsync_CallsBusinessLogic()
    {
        //Arrange
        var id = _fixture.Create<Guid>();

        //Act
        var result = await _controller.RemoveFavouriteAppForCurrentUserAsync(id);

        //Assert
        A.CallTo(() => _logic.RemoveFavouriteAppForUserAsync(id)).MustHaveHappenedOnceExactly();
        Assert.IsType<NoContentResult>(result);
    }

    #region GetCompanySubscribedAppSubscriptionStatusesForCurrentUserAsync

    [Fact]
    public async Task GetCompanySubscribedAppSubscriptionStatusesForCurrentUserAsync_ReturnsExpectedCount()
    {
        //Arrange
        var data = _fixture.CreateMany<OfferSubscriptionStatusDetailData>(3).ToImmutableArray();
        var pagination = new Pagination.Response<OfferSubscriptionStatusDetailData>(new Pagination.Metadata(data.Length, 1, 0, data.Length), data);
        A.CallTo(() => _logic.GetCompanySubscribedAppSubscriptionStatusesForUserAsync(A<int>._, A<int>._, A<OfferSubscriptionStatusId?>._, A<string?>._))
            .Returns(pagination);

        //Act
        var result = await _controller.GetCompanySubscribedAppSubscriptionStatusesForUserAsync();

        //Assert
        A.CallTo(() => _logic.GetCompanySubscribedAppSubscriptionStatusesForUserAsync(0, 15, null, null)).MustHaveHappenedOnceExactly();
        result.Content.Should().HaveCount(3).And.ContainInOrder(data);
    }

    #endregion

    [Theory]
    [InlineData(null)]
    [InlineData("c714b905-9d2a-4cf3-b9f7-10be4eeddfc8")]
    public async Task GetCompanyProvidedAppSubscriptionStatusesForCurrentUserAsync_ReturnsExpectedCount(string? offerIdTxt)
    {
        //Arrange
        var data = _fixture.CreateMany<OfferCompanySubscriptionStatusResponse>(5).ToImmutableArray();
        Guid? offerId = offerIdTxt == null ? null : new Guid(offerIdTxt);
        var pagination = new Pagination.Response<OfferCompanySubscriptionStatusResponse>(new Pagination.Metadata(data.Length, 1, 0, data.Length), data);
        A.CallTo(() => _logic.GetCompanyProvidedAppSubscriptionStatusesForUserAsync(A<int>._, A<int>._, A<SubscriptionStatusSorting?>._, A<OfferSubscriptionStatusId?>._, A<Guid?>._, A<string?>._))
            .Returns(pagination);

        //Act
        var result = await _controller.GetCompanyProvidedAppSubscriptionStatusesForCurrentUserAsync(offerId: offerId);

        //Assert
        A.CallTo(() => _logic.GetCompanyProvidedAppSubscriptionStatusesForUserAsync(0, 15, null, null, offerId, null)).MustHaveHappenedOnceExactly();
        result.Content.Should().HaveCount(5);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("c714b905-9d2a-4cf3-b9f7-10be4eeddfc8")]
    public async Task GetCompanyProvidedAppSubscriptionStatusesForCurrentUserAsync_ReturnsExpectedCount_AndCreatedAt(string? offerIdTxt)
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        Guid? offerId = offerIdTxt == null ? null : new Guid(offerIdTxt);

        var testData = Enumerable.Range(0, 5)
            .Select(i =>
            {
                var createdAt = now.AddDays(i);

                var companySubscriptionStatuses = new List<CompanySubscriptionStatus>
                {
                    new CompanySubscriptionStatus(
                        Guid.NewGuid(),
                        $"Company {i}",
                        Guid.NewGuid(),
                        OfferSubscriptionStatusId.PENDING,
                        "DE",
                        $"BPN00000000000{i}",
                        $"email{i}@example.com",
                        false,
                        createdAt,
                        ProcessStepTypeId.MANUAL_TRIGGER_ACTIVATE_SUBSCRIPTION)
                };

                return new OfferCompanySubscriptionStatusResponse(
                    Guid.NewGuid(),
                    $"Test Offer {i}",
                    companySubscriptionStatuses,
                    Guid.NewGuid());
            })
            .ToImmutableArray();

        var pagination = new Pagination.Response<OfferCompanySubscriptionStatusResponse>(
            new Pagination.Metadata(testData.Length, 1, 0, testData.Length),
            testData);

        A.CallTo(() => _logic.GetCompanyProvidedAppSubscriptionStatusesForUserAsync(
            A<int>._, A<int>._, A<SubscriptionStatusSorting?>._, A<OfferSubscriptionStatusId?>._, A<Guid?>._, A<string?>._))
            .Returns(pagination);

        // Act
        var result = await _controller.GetCompanyProvidedAppSubscriptionStatusesForCurrentUserAsync(offerId: offerId);

        // Assert
        A.CallTo(() => _logic.GetCompanyProvidedAppSubscriptionStatusesForUserAsync(
            0, 15, null, null, offerId, null)).MustHaveHappenedOnceExactly();

        result.Content.Should().HaveCount(5);

        for (var i = 0; i < 5; i++)
        {
            var item = result.Content.ElementAt(i);
            item.CompanySubscriptionStatuses.Should().ContainSingle();

            var status = item.CompanySubscriptionStatuses.Single();
            status.DateCreated.Should().BeCloseTo(now.AddDays(i), TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task AddAppSubscriptionWithConsent_ReturnsExpectedId()
    {
        //Arrange
        var offerSubscriptionId = Guid.NewGuid();
        var consentData = _fixture.CreateMany<OfferAgreementConsentData>(2);
        A.CallTo(() => _logic.AddOwnCompanyAppSubscriptionAsync(A<Guid>._, A<IEnumerable<OfferAgreementConsentData>>._))
            .Returns(offerSubscriptionId);

        //Act
        var serviceId = Guid.NewGuid();
        var result = await _controller.AddCompanyAppSubscriptionAsync(serviceId, consentData);

        //Assert
        A.CallTo(() => _logic.AddOwnCompanyAppSubscriptionAsync(serviceId, consentData)).MustHaveHappenedOnceExactly();
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeclineAppSubscriptionAsync_ReturnsExpected()
    {
        //Arrange
        var offerSubscriptionId = Guid.NewGuid();
        A.CallTo(() => _logic.DeclineAppSubscriptionAsync(A<Guid>._));

        //Act
        var result = await _controller.DeclineAppSubscriptionAsync(offerSubscriptionId);

        //Assert
        A.CallTo(() => _logic.DeclineAppSubscriptionAsync(offerSubscriptionId)).MustHaveHappenedOnceExactly();
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task UnsubscribeCompanyAppSubscription_ReturnsNoContent()
    {
        //Arrange
        var appId = _fixture.Create<Guid>();

        //Act
        var result = await _controller.UnsubscribeCompanyAppSubscriptionAsync(appId);

        //Assert
        A.CallTo(() => _logic.UnsubscribeOwnCompanyAppSubscriptionAsync(appId)).MustHaveHappenedOnceExactly();
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task GetAppDataAsync_ReturnsExpectedCount()
    {
        //Arrange
        var data = _fixture.CreateMany<AllOfferData>(5).ToImmutableArray();
        var paginationResponse = new Pagination.Response<AllOfferData>(new Pagination.Metadata(data.Length, 1, 0, data.Length), data);
        A.CallTo(() => _logic.GetCompanyProvidedAppsDataForUserAsync(0, 15, null, null, null))
            .Returns(paginationResponse);

        //Act
        var result = await _controller.GetAppDataAsync();

        //Assert
        A.CallTo(() => _logic.GetCompanyProvidedAppsDataForUserAsync(0, 15, null, null, null)).MustHaveHappenedOnceExactly();
        result.Content.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetServiceAgreement_ReturnsExpected()
    {
        //Arrange
        var appId = _fixture.Create<Guid>();
        var agreementData = _fixture.CreateMany<AgreementData>(5).ToAsyncEnumerable();
        A.CallTo(() => _logic.GetAppAgreement(A<Guid>._))
            .Returns(agreementData);

        //Act
        var result = await _controller.GetAppAgreement(appId).ToListAsync();

        //Assert
        A.CallTo(() => _logic.GetAppAgreement(appId)).MustHaveHappenedOnceExactly();
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task AutoSetupService_ReturnsExpected()
    {
        //Arrange
        var offerSubscriptionId = Guid.NewGuid();
        var userRoleData = new List<string>();
        userRoleData.Add("Sales Manager");
        userRoleData.Add("IT Manager");
        var data = new OfferAutoSetupData(offerSubscriptionId, "https://test.de");
        var responseData = new OfferAutoSetupResponseData(
            Enumerable.Repeat(new TechnicalUserInfoData(Guid.NewGuid(), userRoleData, "abcPW", "sa1"), 1),
            new ClientInfoData(Guid.NewGuid().ToString(), "http://www.google.com")
        );
        A.CallTo(() => _logic.AutoSetupAppAsync(A<OfferAutoSetupData>._))
            .Returns(responseData);

        //Act
        var result = await _controller.AutoSetupApp(data);

        //Assert
        A.CallTo(() => _logic.AutoSetupAppAsync(data)).MustHaveHappenedOnceExactly();
        Assert.IsType<OfferAutoSetupResponseData>(result);
        result.Should().Be(responseData);
    }

    [Fact]
    public async Task StartAutoSetupProcess_ReturnsExpected()
    {
        //Arrange
        var offerSubscriptionId = Guid.NewGuid();
        var data = new OfferAutoSetupData(offerSubscriptionId, "https://test.de");

        //Act
        var result = await _controller.StartAutoSetupAppProcess(data);

        //Assert
        A.CallTo(() => _logic.StartAutoSetupAsync(data)).MustHaveHappenedOnceExactly();
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task ActivateAppSubscription_ReturnsExpected()
    {
        //Arrange
        var offerSubscriptionId = Guid.NewGuid();

        //Act
        var result = await _controller.ActivateOfferSubscription(offerSubscriptionId);

        //Assert
        A.CallTo(() => _logic.TriggerActivateOfferSubscription(offerSubscriptionId)).MustHaveHappenedOnceExactly();
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task ActivateSingleInstance_ReturnsExpected()
    {
        //Arrange
        var offerSubscriptionId = Guid.NewGuid();

        //Act
        var result = await _controller.ActivateSingleInstance(offerSubscriptionId);

        //Assert
        A.CallTo(() => _logic.ActivateSingleInstance(offerSubscriptionId)).MustHaveHappenedOnceExactly();
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task GetAppImageDocumentContentAsync_ReturnsExpected()
    {
        //Arrange
        var appId = _fixture.Create<Guid>();
        var documentId = _fixture.Create<Guid>();
        var content = _fixture.Create<byte[]>();
        var fileName = _fixture.Create<string>();

        A.CallTo(() => _logic.GetAppDocumentContentAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._))
            .Returns((content, "image/png", fileName));

        //Act
        var result = await _controller.GetAppDocumentContentAsync(appId, documentId, CancellationToken.None);

        //Assert
        A.CallTo(() => _logic.GetAppDocumentContentAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        result.Should().NotBeNull().And.Match<FileResult>(x =>
            x.ContentType == "image/png" &&
            x.FileDownloadName == fileName
        );
    }

    [Fact]
    public async Task GetAppDocumentTypePdfContentAsync_ReturnsExpected()
    {
        //Arrange
        var appId = _fixture.Create<Guid>();
        var documentId = _fixture.Create<Guid>();
        var content = _fixture.Create<byte[]>();
        var fileName = _fixture.Create<string>();

        A.CallTo(() => _logic.GetAppDocumentContentAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._))
            .Returns((content, "application/pdf", fileName));

        //Act
        var result = await _controller.GetAppDocumentContentAsync(appId, documentId, CancellationToken.None);

        //Assert
        A.CallTo(() => _logic.GetAppDocumentContentAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        result.Should().NotBeNull().And.Match<FileResult>(x =>
            x.ContentType == "application/pdf" &&
            x.FileDownloadName == fileName
        );
    }

    [Fact]
    public async Task GetSubscriptionDetailForProvider_ReturnsExpected()
    {
        // Arrange
        var appId = _fixture.Create<Guid>();
        var subscriptionId = _fixture.Create<Guid>();
        var data = _fixture.Create<OfferProviderSubscriptionDetailData>();
        A.CallTo(() => _logic.GetSubscriptionDetailForProvider(appId, subscriptionId))
            .Returns(data);

        // Act
        var result = await _controller.GetSubscriptionDetailForProvider(appId, subscriptionId);

        // Assert
        A.CallTo(() => _logic.GetSubscriptionDetailForProvider(appId, subscriptionId)).MustHaveHappenedOnceExactly();
        result.Should().Be(data);
    }

    [Fact]
    public async Task GetSubscriptionDetailForSubscriber_ReturnsExpected()
    {
        // Arrange
        var appId = _fixture.Create<Guid>();
        var subscriptionId = _fixture.Create<Guid>();
        var data = _fixture.Create<SubscriberSubscriptionDetailData>();
        A.CallTo(() => _logic.GetSubscriptionDetailForSubscriber(appId, subscriptionId))
            .Returns(data);

        // Act
        var result = await _controller.GetSubscriptionDetailForSubscriber(appId, subscriptionId);

        // Assert
        A.CallTo(() => _logic.GetSubscriptionDetailForSubscriber(appId, subscriptionId)).MustHaveHappenedOnceExactly();
        result.Should().Be(data);
    }

    #region GetOwnCompanyActiveSubscribedAppSubscriptionStatusesForUser

    [Fact]
    public async Task GetOwnCompanyActiveSubscribedAppSubscriptionStatusesForUserAsync_ReturnsExpectedCount()
    {
        //Arrange
        var data = _fixture.CreateMany<ActiveOfferSubscriptionStatusData>(3).ToImmutableArray().ToAsyncEnumerable();
        A.CallTo(() => _logic.GetOwnCompanyActiveSubscribedAppSubscriptionStatusesForUserAsync())
            .Returns(data);

        //Act
        var result = await _controller.GetOwnCompanyActiveSubscribedAppSubscriptionStatusesForUserAsync().ToListAsync();

        //Assert
        A.CallTo(() => _logic.GetOwnCompanyActiveSubscribedAppSubscriptionStatusesForUserAsync()).MustHaveHappenedOnceExactly();
        result.Should().HaveCount(3);
    }

    #endregion

    #region GetOwnCompanySubscribedAppOfferSubscriptionDataForUser

    [Fact]
    public async Task GetOwnCompanySubscribedAppOfferSubscriptionDataForUserAsync_ReturnsExpectedCount()
    {
        //Arrange
        var data = _fixture.CreateMany<OfferSubscriptionData>(3).ToImmutableArray().ToAsyncEnumerable();
        A.CallTo(() => _logic.GetOwnCompanySubscribedAppOfferSubscriptionDataForUserAsync())
            .Returns(data);

        //Act
        var result = await _controller.GetOwnCompanySubscribedAppOfferSubscriptionDataForUserAsync().ToListAsync();

        //Assert
        A.CallTo(() => _logic.GetOwnCompanySubscribedAppOfferSubscriptionDataForUserAsync()).MustHaveHappenedOnceExactly();
        result.Should().HaveCount(3);
    }

    #endregion

}
