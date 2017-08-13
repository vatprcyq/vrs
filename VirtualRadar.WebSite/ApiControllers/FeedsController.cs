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
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using InterfaceFactory;
using VirtualRadar.Interface;
using VirtualRadar.Interface.Listener;
using VirtualRadar.Interface.Owin;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.StandingData;
using VirtualRadar.Interface.WebSite;

namespace VirtualRadar.WebSite.ApiControllers
{
    /// <summary>
    /// API methods that deal with aircraft data feeds.
    /// </summary>
    public class FeedsController : PipelineApiController
    {
        /// <summary>
        /// Returns a list of every public facing feed.
        /// </summary>
        /// <returns></returns>
        [HttpGet, Route("api/1.00/feeds")]
        public FeedJson[] GetFeeds()
        {
            var feedManager = Factory.Singleton.Resolve<IFeedManager>().Singleton;
            return feedManager.VisibleFeeds.Select(r => FeedJson.ToModel(r)).Where(r => r != null).ToArray();
        }

        /// <summary>
        /// Returns details for a single feed.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet, Route("api/1.00/feeds/{id}")]
        public FeedJson GetFeed(int id)
        {
            var feedManager = Factory.Singleton.Resolve<IFeedManager>().Singleton;
            return FeedJson.ToModel(feedManager.GetByUniqueId(id, ignoreInvisibleFeeds: true));
        }

        /// <summary>
        /// Returns the polar plot for a feed.
        /// </summary>
        /// <param name="feedId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("api/1.00/feeds/polar-plot/{feedId}")]
        [Route("PolarPlot.json")]                       // pre-version 3 route
        public PolarPlotsJson GetPolarPlot(int feedId = -1)
        {
            var feedManager = Factory.Singleton.Resolve<IFeedManager>().Singleton;
            var feed = feedManager.GetByUniqueId(feedId, ignoreInvisibleFeeds: true);
            var plotter = feed?.AircraftList?.PolarPlotter;

            if(plotter != null && PipelineRequest.IsInternet) {
                var configuration = Factory.Singleton.Resolve<ISharedConfiguration>().Singleton.Get();
                if(!configuration.InternetClientSettings.CanShowPolarPlots) {
                    plotter = null;
                }
            }

            var result = plotter != null ? PolarPlotsJson.ToModel(feed.UniqueId, plotter) : new PolarPlotsJson() { FeedId = feedId, };

            return result;
        }

        /// <summary>
        /// Returns a list of all aircraft on the feed.
        /// </summary>
        /// <param name="feedId">The numeric feed ID. If not supplied then the default feed is used.</param>
        /// <returns></returns>
        [HttpPost]
        [Route("api/1.00/feeds/aircraft-list/{feedId?}")]
        public AircraftListJson AircraftList(GetAircraftListModel model, int feedId = -1)
        {
            return BuildAircraftList(new AircraftListJsonBuilderArgs() {
                BrowserLatitude =       model?.Latitude,
                BrowserLongitude =      model?.Longitude,
                IsFlightSimulatorList = model?.FlightSimulator ?? false,
                PreviousDataVersion =   model?.LastDataVersion ?? -1L,
                ResendTrails =          model?.ResendTrails ?? false,
                SelectedAircraftId =    model?.SelectedAircraft ?? -1,
                ServerTimeTicks =       model?.ServerTicks ?? -1L,
                SourceFeedId =          feedId,
            });
        }

        /// <summary>
        /// Version 2 endpoint for fetching all aircraft on the feed.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("AircraftList.json")]            // pre-version 3 route
        [Route("FlightSimList.json")]           // pre-version 3 route
        public AircraftListJson AircraftListV2([FromUri] int feed = -1, double? lat = null, double? lng = null, long ldv = -1, long stm = -1, byte refreshTrails = 0, int selAc = -1)
        {
            var isFlightSimList = RequestContext.RouteData.Route.RouteTemplate == "FlightSimList.json";

            return BuildAircraftList(new AircraftListJsonBuilderArgs() {
                BrowserLatitude =       lat,
                BrowserLongitude =      lng,
                IsFlightSimulatorList = isFlightSimList,
                PreviousDataVersion =   ldv,
                ResendTrails =          refreshTrails == 1,
                SelectedAircraftId =    selAc,
                ServerTimeTicks =       stm,
                SourceFeedId =          feed,
                Filter =                AircraftListJsonBuilderFilterFromQueryString(),
            });
        }

        private AircraftListJson BuildAircraftList(AircraftListJsonBuilderArgs builderArgs)
        {
            if(builderArgs.IsFlightSimulatorList) {
                builderArgs.AircraftList = Factory.Singleton.Resolve<IFlightSimulatorAircraftList>();
                builderArgs.SourceFeedId = -1;
            }
            builderArgs.IsInternetClient = PipelineRequest.IsInternet;

            var builder = Factory.Singleton.Resolve<IAircraftListJsonBuilder>();
            builder.Initialise(new WebSiteProvider());

            return builder.Build(builderArgs);
        }

        private AircraftListJsonBuilderFilter AircraftListJsonBuilderFilterFromQueryString()
        {
            AircraftListJsonBuilderFilter result = null;

            var query = PipelineContext.Request.Query;
            foreach(var kvp in query.Where(r => r.Key.Length > 3 && (r.Key[0] == 'f' || r.Key[0] == 'F'))) {
                var key = kvp.Key.ToUpper();
                var value = kvp.Value == null || kvp.Value.Length < 1 ? "" : kvp.Value[0] ?? "";
                switch(key.Substring(0, 3)) {
                    case "FAI":     result = DecodeStringFilter     ("FAIR",    key, value, result, (f,v) => f.Airport = v); break;
                    case "FCA":     result = DecodeStringFilter     ("FCALL",   key, value, result, (f,v) => f.Callsign = v); break;
                    case "FCO":     result = DecodeStringFilter     ("FCOU",    key, value, result, (f,v) => f.Icao24Country = v); break;
                    case "FIC":     result = DecodeStringFilter     ("FICO",    key, value, result, (f,v) => f.Icao24 = v); break;
                    case "FOP":     result = DecodeStringFilter     ("FOPICAO", key, value, result, (f,v) => f.OperatorIcao = v);
                                    result = DecodeStringFilter     ("FOP",     key, value, result, (f,v) => f.Operator = v); break;
                    case "FRE":     result = DecodeStringFilter     ("FREG",    key, value, result, (f,v) => f.Registration = v); break;
                    case "FTY":     result = DecodeStringFilter     ("FTYP",    key, value, result, (f,v) => f.Type = v); break;
                    case "FUT":     result = DecodeStringFilter     ("FUT",     key, value, result, (f,v) => f.UserTag = v); break;

                    case "FIN":     result = DecodeBoolFilter       ("FINT",    key, value, result, (f,v) => f.IsInteresting = v); break;
                    case "FMI":     result = DecodeBoolFilter       ("FMIL",    key, value, result, (f,v) => f.IsMilitary = v); break;
                    case "FNO":     result = DecodeBoolFilter       ("FNOPOS",  key, value, result, (f,v) => f.MustTransmitPosition = v); break;

                    case "FAL":     result = DecodeIntRangeFilter   ("FALT",    key, value, result, f => f.Altitude, (f,v) => f.Altitude = v); break;
                    case "FSQ":     result = DecodeIntRangeFilter   ("FSQK",    key, value, result, f => f.Squawk,   (f,v) => f.Squawk = v); break;
                    case "FDS":     result = DecodeDoubleRangeFilter("FDST",    key, value, result, f => f.Distance, (f,v) => f.Distance = v); break;

                    case "FEG":     result = DecodeEnumFilter<EngineType>            ("FEGT", key, value, result, (f,v) => f.EngineType = v); break;
                    case "FSP":     result = DecodeEnumFilter<Species>               ("FSPC", key, value, result, (f,v) => f.Species = v); break;
                    case "FWT":     result = DecodeEnumFilter<WakeTurbulenceCategory>("FWTC", key, value, result, (f,v) => f.WakeTurbulenceCategory = v); break;
                }
            }

            result = DecodeBounds(result, query["FNBND"], query["FWBND"], query["FSBND"], query["FEBND"]);

            return result;
        }

        private char DecodeFilter<T>(string prefix, T filter, string name)
            where T: Filter
        {
            var suffixLength = name.Length - prefix.Length;

            var ch = !name.StartsWith(prefix) || suffixLength < 1 || suffixLength > 2 ? '\0' : name[name.Length - 1];
            if(ch == 'N' && suffixLength == 2) {
                filter.ReverseCondition = true;
                ch = name[name.Length - 2];
                suffixLength = 1;
            }

            var result = '\0';
            if(suffixLength == 1) {
                result = ch;
                switch(ch) {
                    case 'L':
                    case 'U':   filter.Condition = FilterCondition.Between; break;
                    case 'S':   filter.Condition = FilterCondition.StartsWith; break;
                    case 'E':   filter.Condition = FilterCondition.EndsWith; break;
                    case 'C':   filter.Condition = FilterCondition.Contains; break;
                    case 'Q':   filter.Condition = FilterCondition.Equals; break;
                    default:    result = '\0'; break;
                }
            }

            return result;
        }

        private void DoAssignFilter<T>(ref AircraftListJsonBuilderFilter result, Action<AircraftListJsonBuilderFilter, T> assignFilter, T filter)
        {
            if(result == null) {
                result = new AircraftListJsonBuilderFilter();
            }

            assignFilter(result, filter);
        }

        private AircraftListJsonBuilderFilter DecodeBoolFilter(string prefix, string key, string value, AircraftListJsonBuilderFilter result, Action<AircraftListJsonBuilderFilter, FilterBool> assignFilter)
        {
            var filter = new FilterBool();
            if(DecodeFilter(prefix, filter, key) == 'Q') {
                if(!String.IsNullOrEmpty(value)) {
                    filter.Value = value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
                    DoAssignFilter(ref result, assignFilter, filter);
                }
            }

            return result;
        }

        private AircraftListJsonBuilderFilter DecodeDoubleRangeFilter(string prefix, string key, string value, AircraftListJsonBuilderFilter result, Func<AircraftListJsonBuilderFilter, FilterRange<double>> getFilter, Action<AircraftListJsonBuilderFilter, FilterRange<double>> assignFilter)
        {
            if(double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleValue)) {
                var filter = result == null ? new FilterRange<double>() : getFilter(result);
                switch(DecodeFilter(prefix, filter, key)) {
                    case 'L':   filter.LowerValue = doubleValue; break;
                    case 'U':   filter.UpperValue = doubleValue; break;
                    default:    filter = null; break;
                }
                if(filter != null) {
                    DoAssignFilter(ref result, assignFilter, filter);
                }
            }

            return result;
        }

        private AircraftListJsonBuilderFilter DecodeEnumFilter<T>(string prefix, string key, string value, AircraftListJsonBuilderFilter result, Action<AircraftListJsonBuilderFilter, FilterEnum<T>> assignFilter)
            where T: struct, IComparable
        {
            if(!String.IsNullOrEmpty(value) && int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int intValue)) {
                if(Enum.IsDefined(typeof(T), intValue)) {
                    var filter = new FilterEnum<T>();
                    filter.Value = (T)((object)intValue);
                    if(DecodeFilter(prefix, filter, key) == 'Q') {
                        DoAssignFilter(ref result, assignFilter, filter);
                    }
                }
            }

            return result;
        }

        private AircraftListJsonBuilderFilter DecodeIntRangeFilter(string prefix, string key, string value, AircraftListJsonBuilderFilter result, Func<AircraftListJsonBuilderFilter, FilterRange<int>> getFilter, Action<AircraftListJsonBuilderFilter, FilterRange<int>> assignFilter)
        {
            if(int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int intValue)) {
                var filter = result == null ? new FilterRange<int>() : getFilter(result);
                switch(DecodeFilter(prefix, filter, key)) {
                    case 'L':   filter.LowerValue = intValue; break;
                    case 'U':   filter.UpperValue = intValue; break;
                    default:    filter = null; break;
                }
                if(filter != null) {
                    DoAssignFilter(ref result, assignFilter, filter);
                }
            }

            return result;
        }

        private AircraftListJsonBuilderFilter DecodeStringFilter(string prefix, string key, string value, AircraftListJsonBuilderFilter result, Action<AircraftListJsonBuilderFilter, FilterString> assignFilter)
        {
            var filter = new FilterString();
            switch(DecodeFilter(prefix, filter, key)) {
                case 'C':
                case 'E':
                case 'Q':
                case 'S':
                    filter.Value = value;
                    DoAssignFilter(ref result, assignFilter, filter);
                    break;
            }

            return result;
        }

        private AircraftListJsonBuilderFilter DecodeBounds(AircraftListJsonBuilderFilter result, string northText, string westText, string southText, string eastText)
        {
            if(!String.IsNullOrEmpty(northText) && !String.IsNullOrEmpty(westText) && !String.IsNullOrEmpty(southText) && !String.IsNullOrEmpty(eastText)) {
                if(double.TryParse(northText, NumberStyles.Any, CultureInfo.InvariantCulture, out double north) &&
                   double.TryParse(southText, NumberStyles.Any, CultureInfo.InvariantCulture, out double south) &&
                   double.TryParse(westText,  NumberStyles.Any, CultureInfo.InvariantCulture, out double west) &&
                   double.TryParse(eastText,  NumberStyles.Any, CultureInfo.InvariantCulture, out double east)) {
                    if(result == null) {
                        result = new AircraftListJsonBuilderFilter();
                    }
                    result.PositionWithin = new Pair<Coordinate>(
                        new Coordinate(north, west),
                        new Coordinate(south, east)
                    );
                }
            }

            return result;
        }
    }
}
