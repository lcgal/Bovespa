﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Logging;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators.Factories
{
    /// <summary>
    /// Helper class used to create the corporate event providers
    /// <see cref="MappingEventProvider"/>, <see cref="SplitEventProvider"/>,
    /// <see cref="DividendEventProvider"/>, <see cref="DelistingEventProvider"/>
    /// </summary>
    public static class CorporateEventEnumeratorFactory
    {
        /// <summary>
        /// Creates a new <see cref="AuxiliaryDataEnumerator"/> that will hold the
        /// corporate event providers
        /// </summary>
        /// <param name="rawDataEnumerator">The underlying raw data enumerator</param>
        /// <param name="config">The <see cref="SubscriptionDataConfig"/></param>
        /// <param name="factorFileProvider">Used for getting factor files</param>
        /// <param name="tradableDayNotifier">Tradable dates provider</param>
        /// <param name="mapFileResolver">Used for resolving the correct map files</param>
        /// <param name="includeAuxiliaryData">True to emit auxiliary data</param>
        /// <param name="startTime">Start date for the data request</param>
        /// <param name="enablePriceScaling">Applies price factor</param>
        /// <returns>The new auxiliary data enumerator</returns>
        public static IEnumerator<BaseData> CreateEnumerators(
            IEnumerator<BaseData> rawDataEnumerator,
            SubscriptionDataConfig config,
            IFactorFileProvider factorFileProvider,
            ITradableDatesNotifier tradableDayNotifier,
            MapFileResolver mapFileResolver,
            bool includeAuxiliaryData,
            DateTime startTime,
            bool enablePriceScaling = true)
        {
            var lazyFactorFile =
                new Lazy<FactorFile>(() => SubscriptionUtils.GetFactorFileToUse(config, factorFileProvider));

            var tradableEventProviders = new List<ITradableDateEventProvider>();
            if (config.Symbol.SecurityType != SecurityType.FutureOption)
            {
                // Maintain order of the old event providers to avoid any sort of potential
                // non-deterministic errors from occurring
                tradableEventProviders.Add(new MappingEventProvider());
                tradableEventProviders.Add(new SplitEventProvider());
                tradableEventProviders.Add(new DividendEventProvider());
                tradableEventProviders.Add(new DelistingEventProvider());
            }
            else
            {
                tradableEventProviders.Add(new DelistingEventProvider());
            }

            var enumerator = new AuxiliaryDataEnumerator(
                config,
                lazyFactorFile,
                new Lazy<MapFile>(() => GetMapFileToUse(config, mapFileResolver)),
                tradableEventProviders.ToArray(),
                tradableDayNotifier,
                includeAuxiliaryData,
                startTime);

            // avoid price scaling for backtesting; calculate it directly in worker
            // and allow subscription to extract the the data depending on config data mode
            var dataEnumerator = rawDataEnumerator;
            if (enablePriceScaling)
            {
                dataEnumerator = new PriceScaleFactorEnumerator(
                    rawDataEnumerator,
                    config,
                    lazyFactorFile);
            }

            return new SynchronizingEnumerator(dataEnumerator, enumerator);
        }

        /// <summary>
        /// Centralized logic used by the data feeds to determine if we should emit auxiliary base data points.
        /// For equities we only want to emit split/dividends events for non internal and only for <see cref="TradeBar"/> configurations
        /// this last part is because equities also have <see cref="QuoteBar"/> subscriptions.
        /// </summary>
        /// <remarks>The <see cref="TimeSliceFactory"/> does not allow for multiple dividends/splits per symbol in the same time slice
        /// but we don't want to rely only on that and make an explicit decision here.</remarks>
        /// <remarks>History provider is never emitting auxiliary data points</remarks>
        public static bool ShouldEmitAuxiliaryBaseData(SubscriptionDataConfig config)
        {
            return config.SecurityType != SecurityType.Equity || !config.IsInternalFeed
                && (config.Type == typeof(TradeBar) || config.Type == typeof(Tick) && config.TickType == TickType.Trade);
        }

        private static MapFile GetMapFileToUse(
            SubscriptionDataConfig config,
            MapFileResolver mapFileResolver)
        {
            var mapFileToUse = new MapFile(config.Symbol.Value, new List<MapFileRow>());

            // load up the map and factor files for equities, options, and custom data
            if (config.TickerShouldBeMapped())
            {
                try
                {
                    var mapFile = mapFileResolver.ResolveMapFile(config.Symbol, config.Type);

                    // only take the resolved map file if it has data, otherwise we'll use the empty one we defined above
                    if (mapFile.Any())
                    {
                        mapFileToUse = mapFile;
                    }
                }
                catch (Exception err)
                {
                    Log.Error(err, "CorporateEventEnumeratorFactory.GetMapFileToUse():" +
                        " Map File: " + config.Symbol.ID + ": ");
                }
            }

            return mapFileToUse;
        }
    }
}
