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
using QuantConnect.Orders;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Base parameters used by <see cref="LiveResultParameters"/> and <see cref="BacktestResultParameters"/>
    /// </summary>
    public class BaseResultParameters
    {
        /// <summary>
        /// Contains population averages scores over the life of the algorithm
        /// </summary>
        public AlphaRuntimeStatistics AlphaRuntimeStatistics { get; set; }

        /// <summary>
        /// Trade profit and loss information since the last algorithm result packet
        /// </summary>
        public IDictionary<DateTime, decimal> ProfitLoss { get; set; }

        /// <summary>
        /// Charts updates for the live algorithm since the last result packet
        /// </summary>
        public IDictionary<string, Chart> Charts { get; set; }

        /// <summary>
        /// Order updates since the last result packet
        /// </summary>
        public IDictionary<int, Order> Orders { get; set; }

        /// <summary>
        /// Order events updates since the last result packet
        /// </summary>
        public List<OrderEvent> OrderEvents { get; set; }

        /// <summary>
        /// Statistics information sent during the algorithm operations.
        /// </summary>
        public IDictionary<string, string> Statistics { get; set; }

        /// <summary>
        /// Runtime banner/updating statistics in the title banner of the live algorithm GUI.
        /// </summary>
        public IDictionary<string, string> RuntimeStatistics { get; set; }
    }
}
