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
*/

using System;
using QuantConnect.Orders.Fees;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Represents a simple option margin model.
    /// </summary>
    /// <remarks>
    /// Options are not traded on margin. Margin requirements exist though for those portfolios with short positions.
    /// Current implementation covers only single long/naked short option positions.
    /// </remarks>
    public class OptionMarginModel : SecurityMarginModel
    {
        // initial margin
        private const decimal OptionMarginRequirement = 1;
        private const decimal NakedPositionMarginRequirement = 0.1m;
        private const decimal NakedPositionMarginRequirementOtm = 0.2m;

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionMarginModel"/>
        /// </summary>
        /// <param name="requiredFreeBuyingPowerPercent">The percentage used to determine the required unused buying power for the account.</param>
        public OptionMarginModel(decimal requiredFreeBuyingPowerPercent = 0)
        {
            RequiredFreeBuyingPowerPercent = requiredFreeBuyingPowerPercent;
        }

        /// <summary>
        /// Gets the current leverage of the security
        /// </summary>
        /// <param name="security">The security to get leverage for</param>
        /// <returns>The current leverage in the security</returns>
        public override decimal GetLeverage(Security security)
        {
            // Options are not traded on margin
            return 1;
        }

        /// <summary>
        /// Sets the leverage for the applicable securities, i.e, options.
        /// </summary>
        /// <param name="security"></param>
        /// <param name="leverage">The new leverage</param>
        public override void SetLeverage(Security security, decimal leverage)
        {
            // Options are leveraged products and different leverage cannot be set by user.
            throw new InvalidOperationException("Options are leveraged products and different leverage cannot be set by user");
        }

        /// <summary>
        /// Gets the total margin required to execute the specified order in units of the account currency including fees
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the order</param>
        /// <returns>The total margin in terms of the currency quoted in the order</returns>
        protected override decimal GetInitialMarginRequiredForOrder(
            InitialMarginRequiredForOrderParameters parameters)
        {
            //Get the order value from the non-abstract order classes (MarketOrder, LimitOrder, StopMarketOrder)
            //Market order is approximated from the current security price and set in the MarketOrder Method in QCAlgorithm.

            var fees = parameters.Security.FeeModel.GetOrderFee(
                new OrderFeeParameters(parameters.Security,
                    parameters.Order)).Value;
            var feesInAccountCurrency = parameters.CurrencyConverter.
                ConvertToAccountCurrency(fees).Amount;

            var value = parameters.Order.GetValue(parameters.Security);
            var orderMargin = value * GetMarginRequirement(parameters.Security, value);

            return orderMargin + Math.Sign(orderMargin) * feesInAccountCurrency;
        }

        /// <summary>
        /// Gets the margin currently alloted to the specified holding
        /// </summary>
        /// <param name="security">The security to compute maintenance margin for</param>
        /// <returns>The maintenance margin required for the </returns>
        protected override decimal GetMaintenanceMargin(Security security)
        {
            return security.Holdings.AbsoluteHoldingsCost * GetMaintenanceMarginRequirement(security, security.Holdings.HoldingsCost);
        }

        /// <summary>
        /// The margin that must be held in order to increase the position by the provided quantity
        /// </summary>
        protected override decimal GetInitialMarginRequirement(Security security, decimal quantity)
        {
            var value = security.QuoteCurrency.ConversionRate
                        * security.SymbolProperties.ContractMultiplier
                        * security.Price
                        * quantity;
            return value * GetMarginRequirement(security, value);
        }

        /// <summary>
        /// The percentage of the holding's absolute cost that must be held in free cash in order to avoid a margin call
        /// </summary>
        private decimal GetMaintenanceMarginRequirement(Security security, decimal holding)
        {
            return GetMarginRequirement(security, holding);
        }

        /// <summary>
        /// Private method takes option security and its holding and returns required margin. Method considers all short positions naked.
        /// </summary>
        /// <param name="security">Option security</param>
        /// <param name="value">Holding value</param>
        /// <returns></returns>
        private decimal GetMarginRequirement(Security security, decimal value)
        {
            var option = (Option) security;

            if (value == 0m ||
                option.Close == 0m ||
                option.StrikePrice == 0m ||
                option.Underlying == null ||
                option.Underlying.Close == 0m)
            {
                return 0m;
            }

            if (value > 0m)
            {
                return OptionMarginRequirement;
            }

            var absValue = -value;
            var optionProperties = (OptionSymbolProperties) option.SymbolProperties;
            var underlying = option.Underlying;

            // inferring ratios of the option and its underlying to get underlying security value
            var multiplierRatio = underlying.SymbolProperties.ContractMultiplier / optionProperties.ContractMultiplier;
            var quantityRatio = optionProperties.ContractUnitOfTrade;
            var priceRatio = underlying.Close / (absValue / quantityRatio);
            var underlyingValueRatio = multiplierRatio * quantityRatio * priceRatio;

            // calculating underlying security value less out-of-the-money amount
            var amountOTM = option.Right == OptionRight.Call
                ? Math.Max(0, option.StrikePrice - underlying.Close)
                : Math.Max(0, underlying.Close - option.StrikePrice);
            var priceRatioOTM = amountOTM / (absValue / quantityRatio);
            var underlyingValueRatioOTM = multiplierRatio * quantityRatio * priceRatioOTM;

            return OptionMarginRequirement +
                   option.Holdings.AbsoluteQuantity * Math.Max(NakedPositionMarginRequirement * underlyingValueRatio,
                       NakedPositionMarginRequirementOtm * underlyingValueRatio - underlyingValueRatioOTM);
        }
    }
}