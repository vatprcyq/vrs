﻿// Copyright © 2017 onwards, Andrew Whewell
// All rights reserved.
//
// Redistribution and use of this software in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//    * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//    * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//    * Neither the name of the author nor the names of the program's contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHORS OF THE SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using InterfaceFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Test.Framework;
using VirtualRadar.Interface;
using VirtualRadar.Interface.BaseStation;
using VirtualRadar.Interface.Listener;
using VirtualRadar.Interface.Owin;
using VirtualRadar.Interface.StandingData;
using VirtualRadar.Interface.WebSite;

namespace Test.VirtualRadar.WebSite.ApiControllers
{
    [TestClass]
    public class FeedsControllerTests : ControllerTests
    {
        private Mock<IFeedManager> _FeedManager;
        private List<Mock<IFeed>> _VisibleFeeds;
        private Mock<IBaseStationAircraftList> _AircraftList;
        private Mock<IPolarPlotter> _PolarPlotter;
        private List<PolarPlotSlice> _Slices;
        private Mock<IAircraftListJsonBuilder> _AircraftListJsonBuilder;
        private AircraftListJson _AircraftListJson;
        private AircraftListJsonBuilderArgs _ActualAircraftListJsonBuilderArgs;
        private Mock<IFlightSimulatorAircraftList> _FlightSimulatorAircraftList;
        private HttpContent _EmptyPostBody;

        protected override void ExtraInitialise()
        {
            _Configuration.InternetClientSettings.CanShowPolarPlots = true;

            _Slices = new List<PolarPlotSlice>();

            _PolarPlotter = TestUtilities.CreateMockInstance<IPolarPlotter>();
            _PolarPlotter.Setup(r => r.TakeSnapshot()).Returns(_Slices);

            _AircraftList = TestUtilities.CreateMockInstance<IBaseStationAircraftList>();
            _AircraftList.SetupGet(r => r.PolarPlotter).Returns(_PolarPlotter.Object);

            _FlightSimulatorAircraftList = TestUtilities.CreateMockImplementation<IFlightSimulatorAircraftList>();

            _AircraftListJson = new AircraftListJson();
            _ActualAircraftListJsonBuilderArgs = null;
            _AircraftListJsonBuilder = TestUtilities.CreateMockImplementation<IAircraftListJsonBuilder>();
            _AircraftListJsonBuilder
                .Setup(r => r.Build(It.IsAny<AircraftListJsonBuilderArgs>()))
                .Returns((AircraftListJsonBuilderArgs args) => {
                    _ActualAircraftListJsonBuilderArgs = args;
                    return _AircraftListJson;
                });

            _FeedManager = TestUtilities.CreateMockSingleton<IFeedManager>();
            _VisibleFeeds = new List<Mock<IFeed>>();
            _FeedManager.Setup(r => r.GetByUniqueId(It.IsAny<int>(), It.IsAny<bool>())).Returns((IFeed)null);
            _FeedManager.SetupGet(r => r.VisibleFeeds).Returns(() => {
                return _VisibleFeeds.Select(r => r.Object).ToArray();
            });

            _EmptyPostBody = new ByteArrayContent(new byte[0]);
        }

        private Mock<IFeed> CreateFeed(int uniqueId = 1, string name = "My Feed", bool hasPlotter = true, bool isVisible = true, bool hasDistinctAircraftList = false)
        {
            var result = TestUtilities.CreateMockInstance<IFeed>();
            result.SetupGet(r => r.UniqueId).Returns(uniqueId);
            result.SetupGet(r => r.Name).Returns(name);
            result.SetupGet(r => r.IsVisible).Returns(isVisible);

            if(hasDistinctAircraftList) {
                var aircraftList = TestUtilities.CreateMockInstance<IBaseStationAircraftList>();
                aircraftList.SetupGet(r => r.PolarPlotter).Returns(hasPlotter ? TestUtilities.CreateMockInstance<IPolarPlotter>().Object : null);
                result.SetupGet(r => r.AircraftList).Returns(aircraftList.Object);
            } else {
                if(!hasPlotter) {
                    _AircraftList.SetupGet(r => r.PolarPlotter).Returns((IPolarPlotter)null);
                }
                result.SetupGet(r => r.AircraftList).Returns(_AircraftList.Object);
            }

            return result;
        }

        private PolarPlotSlice CreatePolarPlotSlice(int lowAltitude, int highAltitude, params PolarPlot[] points)
        {
            var result = new PolarPlotSlice() {
                AltitudeLower =     lowAltitude,
                AltitudeHigher =    highAltitude,
            };
            foreach(var point in points) {
                result.PolarPlots.Add(point.Angle, point);
            }

            return result;
        }

        private Mock<IFeed> ConfigureGetFeedById(Mock<IFeed> feed)
        {
            _FeedManager.Setup(r => r.GetByUniqueId(feed.Object.UniqueId, true)).Returns(feed.Object);

            return feed;
        }

        private AircraftListJsonBuilderArgs ExpectedAircraftListJsonBuilderArgs(
            int feedId = -1,
            double? latitude = null,
            double? longitude = null,
            long previousDataVersion = -1,
            bool resendTrails = false,
            int selectedAircraftID = -1,
            long serverTimeTicks = -1,
            bool isFlightSimulator = false,
            AircraftListJsonBuilderFilter filter = null
        )
        {
            return new AircraftListJsonBuilderArgs() {
                AircraftList =          isFlightSimulator ? _FlightSimulatorAircraftList.Object : null,
                BrowserLatitude =       latitude,
                BrowserLongitude =      longitude,
                Filter =                filter,
                IsFlightSimulatorList = isFlightSimulator,
                PreviousDataVersion =   previousDataVersion,
                ResendTrails =          resendTrails,
                SelectedAircraftId =    selectedAircraftID,
                ServerTimeTicks =       serverTimeTicks,
                SourceFeedId =          feedId,
            };
        }

        private void AssertBuilderArgsAreEqual(AircraftListJsonBuilderArgs expected, AircraftListJsonBuilderArgs actual)
        {
            if(expected == null && actual == null) {
                return;
            }

            if(expected == null && actual != null) {
                Assert.Fail("Expected null args but actual was not null (builder was called when it should not have been)");
            }
            if(expected != null && actual == null) {
                Assert.Fail("Expected args but actual was null (builder was not called when it should have been)");
            }

            Assert.AreSame (expected.AircraftList,          actual.AircraftList);
            Assert.AreEqual(expected.BrowserLatitude,       actual.BrowserLatitude);
            Assert.AreEqual(expected.BrowserLongitude,      actual.BrowserLongitude);
            Assert.AreEqual(expected.IsFlightSimulatorList, actual.IsFlightSimulatorList);
            Assert.AreEqual(expected.PreviousDataVersion,   actual.PreviousDataVersion);
            Assert.AreEqual(expected.ResendTrails,          actual.ResendTrails);
            Assert.AreEqual(expected.SelectedAircraftId,    actual.SelectedAircraftId);
            Assert.AreEqual(expected.ServerTimeTicks,       actual.ServerTimeTicks);
            Assert.AreEqual(expected.SourceFeedId,          actual.SourceFeedId);

            AssertFiltersAreEqual(expected.Filter, actual.Filter);
        }

        private void AssertFiltersAreEqual(AircraftListJsonBuilderFilter expected, AircraftListJsonBuilderFilter actual)
        {
            if(expected == null && actual == null) {
                return;
            }

            if(expected == null && actual != null) {
                Assert.Fail("Expected null filter but actual was not null (filters were configured when they should not have been)");
            }
            if(expected != null && actual == null) {
                Assert.Fail("Expected filter but actual was null (filters were not configured when they should have been)");
            }

            Assert.AreEqual(expected.Airport,                   actual.Airport);
            Assert.AreEqual(expected.Altitude,                  actual.Altitude);
            Assert.AreEqual(expected.Callsign,                  actual.Callsign);
            Assert.AreEqual(expected.Distance,                  actual.Distance);
            Assert.AreEqual(expected.EngineType,                actual.EngineType);
            Assert.AreEqual(expected.Icao24,                    actual.Icao24);
            Assert.AreEqual(expected.Icao24Country,             actual.Icao24Country);
            Assert.AreEqual(expected.IsInteresting,             actual.IsInteresting);
            Assert.AreEqual(expected.IsMilitary,                actual.IsMilitary);
            Assert.AreEqual(expected.MustTransmitPosition,      actual.MustTransmitPosition);
            Assert.AreEqual(expected.Operator,                  actual.Operator);
            Assert.AreEqual(expected.OperatorIcao,              actual.OperatorIcao);
            Assert.AreEqual(expected.PositionWithin,            actual.PositionWithin);
            Assert.AreEqual(expected.Registration,              actual.Registration);
            Assert.AreEqual(expected.Species,                   actual.Species);
            Assert.AreEqual(expected.Squawk,                    actual.Squawk);
            Assert.AreEqual(expected.Type,                      actual.Type);
            Assert.AreEqual(expected.UserTag,                   actual.UserTag);
            Assert.AreEqual(expected.WakeTurbulenceCategory,    actual.WakeTurbulenceCategory);
        }

        #region GetFeeds
        [TestMethod]
        public async Task FeedsController_GetFeeds_Returns_All_Visible_Feeds()
        {
            _VisibleFeeds.Add(CreateFeed(uniqueId: 1, name: "First", hasPlotter: true, hasDistinctAircraftList: true));
            _VisibleFeeds.Add(CreateFeed(uniqueId: 2, name: "Second", hasPlotter: false, hasDistinctAircraftList: true));

            var response = await _Server.HttpClient.GetAsync("/api/1.00/feeds");
            var content = await response.Content.ReadAsStringAsync();
            var feeds = JsonConvert.DeserializeObject<FeedJson[]>(content);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(2, feeds.Length);

            var feed1 = feeds.Single(r => r.UniqueId == 1);
            Assert.AreEqual("First", feed1.Name);
            Assert.AreEqual(true, feed1.HasPolarPlot);

            var feed2 = feeds.Single(r => r.UniqueId == 2);
            Assert.AreEqual("Second", feed2.Name);
            Assert.AreEqual(false, feed2.HasPolarPlot);
        }

        [TestMethod]
        public async Task FeedsController_GetFeeds_Returns_Empty_Array_If_All_Feeds_Are_Invisible()
        {
            var response = await _Server.HttpClient.GetAsync("/api/1.00/feeds");
            var content = await response.Content.ReadAsStringAsync();
            var feeds = JsonConvert.DeserializeObject<FeedJson[]>(content);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(0, feeds.Length);
        }
        #endregion

        #region GetFeed
        [TestMethod]
        public async Task FeedsController_GetFeed_Returns_Feed_If_Known()
        {
            ConfigureGetFeedById(CreateFeed(uniqueId: 1, name: "Feed", hasPlotter: true, isVisible: true));

            var response = await _Server.HttpClient.GetAsync("/api/1.00/feeds/1");
            var content = await response.Content.ReadAsStringAsync();
            var feed = JsonConvert.DeserializeObject<FeedJson>(content);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, feed.UniqueId);
            Assert.AreEqual("Feed", feed.Name);
            Assert.AreEqual(true, feed.HasPolarPlot);
        }

        [TestMethod]
        public async Task FeedsController_GetFeed_Returns_Null_If_ID_Is_Unknown()
        {
            ConfigureGetFeedById(CreateFeed(uniqueId: 1));

            var response = await _Server.HttpClient.GetAsync("/api/1.00/feeds/2");
            var content = await response.Content.ReadAsStringAsync();
            var feed = JsonConvert.DeserializeObject<FeedJson>(content);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNull(feed);
        }

        [TestMethod]
        public async Task FeedsController_GetFeed_Returns_Null_If_Feed_Is_Invisible()
        {
            ConfigureGetFeedById(CreateFeed(uniqueId: 1, isVisible: false));

            var response = await _Server.HttpClient.GetAsync("/api/1.00/feeds/1");
            var content = await response.Content.ReadAsStringAsync();
            var feed = JsonConvert.DeserializeObject<FeedJson>(content);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNull(feed);
        }
        #endregion

        #region GetPolarPlot
        [TestMethod]
        public async Task FeedsController_GetPolarPlot_Returns_Object()
        {
            ConfigureGetFeedById(CreateFeed());

            var response = await _Server.HttpClient.GetAsync("/api/1.00/feeds/polar-plot/1");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<PolarPlotsJson>(content);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(json);
            Assert.AreEqual(1, json.FeedId);
            Assert.IsNotNull(json.Slices);
        }

        [TestMethod]
        public async Task FeedsController_GetPolarPlot_Returns_Object_For_Version_2_Route()
        {
            ConfigureGetFeedById(CreateFeed());

            var response = await _Server.HttpClient.GetAsync("/PolarPlot.json?feedId=1");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<PolarPlotsJson>(content);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(json);
            Assert.AreEqual(1, json.FeedId);
            Assert.IsNotNull(json.Slices);
        }

        [TestMethod]
        public async Task FeedsController_GetPolarPlot_Returns_Slices()
        {
            ConfigureGetFeedById(CreateFeed(uniqueId: 2));
            _Slices.Add(CreatePolarPlotSlice(100, 199, new PolarPlot() { Angle = 1, Altitude = 150, Distance = 7, Latitude = 10.1, Longitude = 11.2 }));

            var response = await _Server.HttpClient.GetAsync("/api/1.00/feeds/polar-plot/2");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<PolarPlotsJson>(content);

            Assert.AreEqual(2, json.FeedId);
            Assert.AreEqual(1, json.Slices.Count);

            var slice = json.Slices[0];
            Assert.AreEqual(100, slice.StartAltitude);
            Assert.AreEqual(199, slice.FinishAltitude);
            Assert.AreEqual(1, slice.Plots.Count);
            Assert.AreEqual(10.1F, slice.Plots[0].Latitude);
            Assert.AreEqual(11.2F, slice.Plots[0].Longitude);
        }

        [TestMethod]
        public async Task FeedsController_GetPolarPlot_Returns_No_Slices_If_Invalid_ID_Supplied()
        {
            ConfigureGetFeedById(CreateFeed(uniqueId: 2));

            var response = await _Server.HttpClient.GetAsync("/api/1.00/feeds/polar-plot/1");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<PolarPlotsJson>(content);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, json.FeedId);
            Assert.AreEqual(0, json.Slices.Count);
        }

        [TestMethod]
        public async Task FeedsController_GetPolarPlot_Returns_No_Slices_If_Feed_Is_Invisible()
        {
            ConfigureGetFeedById(CreateFeed(uniqueId: 1, isVisible: false));

            var response = await _Server.HttpClient.GetAsync("/api/1.00/feeds/polar-plot/1");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<PolarPlotsJson>(content);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, json.FeedId);
            Assert.AreEqual(0, json.Slices.Count);
        }

        [TestMethod]
        public async Task FeedsController_GetPolarPlot_Returns_No_Slices_If_Feed_Has_No_Aircraft_List()
        {
            var feed = CreateFeed(uniqueId: 1);
            feed.SetupGet(r => r.AircraftList).Returns((IBaseStationAircraftList)null);
            ConfigureGetFeedById(feed);

            var response = await _Server.HttpClient.GetAsync("/api/1.00/feeds/polar-plot/1");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<PolarPlotsJson>(content);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, json.FeedId);
            Assert.AreEqual(0, json.Slices.Count);
        }

        [TestMethod]
        public async Task FeedsController_GetPolarPlot_Returns_No_Slices_If_Feed_Has_No_Plotter()
        {
            ConfigureGetFeedById(CreateFeed(uniqueId: 1, hasPlotter: false));

            var response = await _Server.HttpClient.GetAsync("/api/1.00/feeds/polar-plot/1");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<PolarPlotsJson>(content);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, json.FeedId);
            Assert.AreEqual(0, json.Slices.Count);
        }

        [TestMethod]
        public async Task FeedsController_GetPolarPlot_Returns_No_Slices_If_Prohibited_By_Configuration()
        {
            _Configuration.InternetClientSettings.CanShowPolarPlots = false;
            _RemoteIpAddress = "1.2.3.4";

            ConfigureGetFeedById(CreateFeed(uniqueId: 1));
            _Slices.Add(CreatePolarPlotSlice(100, 199, new PolarPlot() { Angle = 1, Altitude = 150, Distance = 7, Latitude = 10.1, Longitude = 11.2 }));

            var response = await _Server.HttpClient.GetAsync("/api/1.00/feeds/polar-plot/1");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<PolarPlotsJson>(content);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, json.FeedId);
            Assert.AreEqual(0, json.Slices.Count);
        }

        [TestMethod]
        public async Task FeedsController_GetPolarPlot_Returns_Slices_If_Prohibited_By_Configuration_But_Accessed_Locally()
        {
            _Configuration.InternetClientSettings.CanShowPolarPlots = false;
            _RemoteIpAddress = "192.168.0.1";

            ConfigureGetFeedById(CreateFeed(uniqueId: 1));
            _Slices.Add(CreatePolarPlotSlice(100, 199, new PolarPlot() { Angle = 1, Altitude = 150, Distance = 7, Latitude = 10.1, Longitude = 11.2 }));

            var response = await _Server.HttpClient.GetAsync("/api/1.00/feeds/polar-plot/1");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<PolarPlotsJson>(content);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, json.FeedId);
            Assert.AreEqual(1, json.Slices.Count);
        }
        #endregion

        #region AircraftList
        [TestMethod]
        public async Task FeedsController_AircraftList_Returns_Default_Aircraft_List_V2()
        {
            var response = await _Server.HttpClient.PostAsync("AircraftList.json", _EmptyPostBody);

            var expected = ExpectedAircraftListJsonBuilderArgs();
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Returns_Default_Aircraft_List_V3()
        {
            var response = await _Server.HttpClient.PostAsync("/api/1.00/feeds/aircraft-list", _EmptyPostBody);

            var expected = ExpectedAircraftListJsonBuilderArgs();
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Specify_Feed_V2()
        {
            var response = await _Server.HttpClient.PostAsync("AircraftList.json?feed=7", _EmptyPostBody);

            var expected = ExpectedAircraftListJsonBuilderArgs(feedId: 7);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Specify_Feed_V3()
        {
            var response = await _Server.HttpClient.PostAsync("/api/1.00/feeds/aircraft-list/7", _EmptyPostBody);

            var expected = ExpectedAircraftListJsonBuilderArgs(feedId: 7);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Specify_Browser_Location_V2()
        {
            var response = await _Server.HttpClient.PostAsync("AircraftList.json?lat=1.2&lng=3.4", _EmptyPostBody);

            var expected = ExpectedAircraftListJsonBuilderArgs(latitude: 1.2, longitude: 3.4);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Specify_Browser_Location_V3()
        {
            var response = await _Server.HttpClient.PostAsync("/api/1.00/feeds/aircraft-list", HttpContentHelper.FormUrlEncoded(new [,] {
                { "Latitude",  "1.2" },
                { "Longitude", "3.4" },
            }));

            var expected = ExpectedAircraftListJsonBuilderArgs(latitude: 1.2, longitude: 3.4);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Identifies_Internet_Clients_V2()
        {
            _RemoteIpAddress = "127.0.0.1";
            await _Server.HttpClient.PostAsync("AircraftList.json", _EmptyPostBody);
            Assert.AreEqual(false, _ActualAircraftListJsonBuilderArgs.IsInternetClient);

            TestCleanup();
            TestInitialise();

            _RemoteIpAddress = "1.2.3.4";
            await _Server.HttpClient.PostAsync("AircraftList.json", _EmptyPostBody);
            Assert.AreEqual(true, _ActualAircraftListJsonBuilderArgs.IsInternetClient);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Identifies_Internet_Clients_V3()
        {
            _RemoteIpAddress = "127.0.0.1";
            await _Server.HttpClient.PostAsync("/api/1.00/feeds/aircraft-list", _EmptyPostBody);
            Assert.AreEqual(false, _ActualAircraftListJsonBuilderArgs.IsInternetClient);

            TestCleanup();
            TestInitialise();

            _RemoteIpAddress = "1.2.3.4";
            await _Server.HttpClient.PostAsync("/api/1.00/feeds/aircraft-list", _EmptyPostBody);
            Assert.AreEqual(true, _ActualAircraftListJsonBuilderArgs.IsInternetClient);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Specify_Last_DataVersion_V2()
        {
            var response = await _Server.HttpClient.PostAsync("AircraftList.json?ldv=12", _EmptyPostBody);

            var expected = ExpectedAircraftListJsonBuilderArgs(previousDataVersion: 12);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Specify_Last_DataVersion_V3()
        {
            var response = await _Server.HttpClient.PostAsync("/api/1.00/feeds/aircraft-list", HttpContentHelper.FormUrlEncoded(new [,] {
                { "LastDataVersion", "12" },
            }));

            var expected = ExpectedAircraftListJsonBuilderArgs(previousDataVersion: 12);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Specify_ServerTimeTicks_V2()
        {
            var response = await _Server.HttpClient.PostAsync("AircraftList.json?stm=12", _EmptyPostBody);

            var expected = ExpectedAircraftListJsonBuilderArgs(serverTimeTicks: 12);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Specify_ServerTimeTicks_V3()
        {
            var response = await _Server.HttpClient.PostAsync("/api/1.00/feeds/aircraft-list", HttpContentHelper.FormUrlEncoded(new [,] {
                { "ServerTicks", "12" },
            }));

            var expected = ExpectedAircraftListJsonBuilderArgs(serverTimeTicks: 12);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Force_Resend_Of_Trails_V2()
        {
            var response = await _Server.HttpClient.PostAsync("AircraftList.json?refreshTrails=1", _EmptyPostBody);

            var expected = ExpectedAircraftListJsonBuilderArgs(resendTrails: true);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Force_Resend_Of_Trails_V3()
        {
            var response = await _Server.HttpClient.PostAsync("/api/1.00/feeds/aircraft-list", HttpContentHelper.FormUrlEncoded(new [,] {
                { "ResendTrails", "true" },
            }));

            var expected = ExpectedAircraftListJsonBuilderArgs(resendTrails: true);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Specify_SelectedAircraft_V2()
        {
            var response = await _Server.HttpClient.PostAsync("AircraftList.json?selAc=8", _EmptyPostBody);

            var expected = ExpectedAircraftListJsonBuilderArgs(selectedAircraftID: 8);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Specify_SelectedAircraft_V3()
        {
            var response = await _Server.HttpClient.PostAsync("/api/1.00/feeds/aircraft-list", HttpContentHelper.FormUrlEncoded(new [,] {
                { "SelectedAircraft", "8" },
            }));

            var expected = ExpectedAircraftListJsonBuilderArgs(selectedAircraftID: 8);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Specify_FSX_AircraftList_V2()
        {
            var response = await _Server.HttpClient.PostAsync("flightsimlist.json", _EmptyPostBody);

            var expected = ExpectedAircraftListJsonBuilderArgs(isFlightSimulator: true);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Can_Specify_FSX_AircraftList_V3()
        {
            var response = await _Server.HttpClient.PostAsync("/api/1.00/feeds/aircraft-list", HttpContentHelper.FormUrlEncoded(new [,] {
                { "FlightSimulator", "true" },
            }));

            var expected = ExpectedAircraftListJsonBuilderArgs(isFlightSimulator: true);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Ignores_Feed_For_FSX_V2()
        {
            var response = await _Server.HttpClient.PostAsync("FLIGHTSimList.json?feed=7", _EmptyPostBody);

            var expected = ExpectedAircraftListJsonBuilderArgs(isFlightSimulator: true, feedId: -1);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Ignores_Feed_For_FSX_V3()
        {
            var response = await _Server.HttpClient.PostAsync("/api/1.00/feeds/aircraft-list/7", HttpContentHelper.FormUrlEncoded(new [,] {
                { "FlightSimulator", "true" },
            }));

            var expected = ExpectedAircraftListJsonBuilderArgs(isFlightSimulator: true, feedId: -1);
            AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "AircraftListFilter$")]
        public async Task FeedsController_AircraftList_Accepts_Filter_Requests_V2()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            var key = HttpUtility.UrlEncode(worksheet.String("Key"));
            var value = HttpUtility.UrlEncode(worksheet.EString("Value"));

            if(!String.IsNullOrEmpty(key)) {
                var queryString = $"{key}={value}";

                var response = await _Server.HttpClient.PostAsync($"AircraftList.json?{queryString}", _EmptyPostBody);

                AircraftListJsonBuilderFilter jsonFilter = null;

                var filterPropertyName = worksheet.String("FilterProperty");
                if(filterPropertyName != null) {
                    jsonFilter = new AircraftListJsonBuilderFilter();

                    var propertyInfo = typeof(AircraftListJsonBuilderFilter).GetProperty(filterPropertyName);
                    var filter = (Filter)Activator.CreateInstance(propertyInfo.PropertyType);
                    filter.Condition = worksheet.ParseEnum<FilterCondition>("Condition");
                    filter.ReverseCondition = worksheet.Bool("ReverseCondition");

                    if(filter is FilterBool filterBool) {
                        filterBool.Value = worksheet.Bool("FilterValue");
                    } else if(filter is FilterEnum<EngineType> filterEngineType) {
                        filterEngineType.Value = worksheet.ParseEnum<EngineType>("FilterValue");
                    } else if(filter is FilterEnum<Species> filterSpecies) {
                        filterSpecies.Value = worksheet.ParseEnum<Species>("FilterValue");
                    } else if(filter is FilterEnum<WakeTurbulenceCategory> filterWakeTurblenceCategory) {
                        filterWakeTurblenceCategory.Value = worksheet.ParseEnum<WakeTurbulenceCategory>("FilterValue");
                    } else if(filter is FilterRange<double> filterDoubleRange) {
                        filterDoubleRange.LowerValue = worksheet.NDouble("LowerValue");
                        filterDoubleRange.UpperValue = worksheet.NDouble("UpperValue");
                    } else if(filter is FilterRange<int> filterIntRange) {
                        filterIntRange.LowerValue = worksheet.NInt("LowerValue");
                        filterIntRange.UpperValue = worksheet.NInt("UpperValue");
                    } else if(filter is FilterString filterString) {
                        filterString.Value = worksheet.EString("FilterValue");
                    } else {
                        Assert.Fail($"Need code to fill {filter.GetType().Name} filters");
                    }

                    propertyInfo.SetValue(jsonFilter, filter);
                }

                var expected = ExpectedAircraftListJsonBuilderArgs(filter: jsonFilter);
                AssertBuilderArgsAreEqual(expected, _ActualAircraftListJsonBuilderArgs);
            }
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "AircraftListFilterName$")]
        public async Task FeedsController_AircraftList_Filters_Ignore_Invalid_Conditions()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            var propertyName = HttpUtility.UrlEncode(worksheet.String("FilterProperty"));
            var queryStringKey = HttpUtility.UrlEncode(worksheet.EString("QueryStringKey"));

            var property = typeof(AircraftListJsonBuilderFilter).GetProperty(propertyName);
            var filterType = property.PropertyType;

            var isStringFilter = typeof(FilterString).IsAssignableFrom(filterType);
            var isIntRangeFilter = typeof(FilterRange<int>).IsAssignableFrom(filterType);
            var isDoubleRangeFilter = typeof(FilterRange<double>).IsAssignableFrom(filterType);
            var isEnumFilter = filterType.IsGenericType && filterType.GetGenericTypeDefinition() == typeof(FilterEnum<>);
            var isBoolFilter = typeof(FilterBool).IsAssignableFrom(filterType);

            var queryStringValue = "1";

            var maybeValidConditions = new string[] { "L", "U", "C", "E", "Q", "S", "LN", "UN", "CN", "EN", "QN", "SN", };
            var alwaysInvalidConditions = new string[] { "NL", "NU", "NC", "NE", "NQ", "NS", "LZ", "UZ", "CZ", "EZ", "QZ", "SZ", "Z", "ZZ", };

            foreach(var condition in maybeValidConditions.Concat(alwaysInvalidConditions)) {
                var queryString = $"{queryStringKey}{condition}={queryStringValue}";
                TestCleanup();
                TestInitialise();

                var conditionIsValid = false;
                switch(condition) {
                    case "L":
                    case "LN":
                    case "U":
                    case "UN":  conditionIsValid = isIntRangeFilter || isDoubleRangeFilter; break;
                    case "C":
                    case "CN":
                    case "E":
                    case "EN":
                    case "S":
                    case "SN":  conditionIsValid = isStringFilter; break;
                    case "Q":
                    case "QN":  conditionIsValid = isStringFilter | isBoolFilter | isEnumFilter; break;
                }

                var response = await _Server.HttpClient.PostAsync($"AircraftList.json?{queryString}", _EmptyPostBody);
                var filterUsed = _ActualAircraftListJsonBuilderArgs.Filter;
                var actualFilter = filterUsed == null ? null : property.GetValue(_ActualAircraftListJsonBuilderArgs.Filter, null);

                if(conditionIsValid) {
                    Assert.IsNotNull(actualFilter, $"Request for {queryString} should have filled {propertyName} but it did not");
                } else {
                    Assert.IsNull(actualFilter, $"Request for {queryString} should not have filled {propertyName} but it did");
                }
            }
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Filters_Accept_Full_Set_Of_Bounds()
        {
            var response = await _Server.HttpClient.PostAsync($"AircraftList.json?FNBnd=1.2&FSBnd=3.4&FWBnd=5.6&FEBnd=7.8", _EmptyPostBody);

            Assert.AreEqual(1.2, _ActualAircraftListJsonBuilderArgs.Filter.PositionWithin.First.Latitude);
            Assert.AreEqual(5.6, _ActualAircraftListJsonBuilderArgs.Filter.PositionWithin.First.Longitude);
            Assert.AreEqual(3.4, _ActualAircraftListJsonBuilderArgs.Filter.PositionWithin.Second.Latitude);
            Assert.AreEqual(7.8, _ActualAircraftListJsonBuilderArgs.Filter.PositionWithin.Second.Longitude);
        }

        [TestMethod]
        public async Task FeedsController_AircraftList_Filters_Ignore_Partial_Set_Of_Bounds()
        {
            var response = await _Server.HttpClient.PostAsync($"AircraftList.json?FNBnd=1.2&FWBnd=5.6", _EmptyPostBody);

            Assert.IsNull(_ActualAircraftListJsonBuilderArgs.Filter);
        }
        #endregion
    }
}
