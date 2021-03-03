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
using System.Collections.Generic;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Orders;
using RestSharp;
using RestSharp.Extensions;
using QuantConnect.Util;

namespace QuantConnect.Api
{
    /// <summary>
    /// QuantConnect.com Interaction Via API.
    /// </summary>
    public class Api : IApi, IDownloadProvider
    {
        private string _dataFolder;

        /// <summary>
        /// Returns the underlying API connection
        /// </summary>
        protected ApiConnection ApiConnection { get; private set; }

        /// <summary>
        /// Initialize the API using the config.json file.
        /// </summary>
        public virtual void Initialize(int userId, string token, string dataFolder)
        {
            ApiConnection = new ApiConnection(userId, token);
            _dataFolder = dataFolder;

            //Allow proper decoding of orders from the API.
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Converters = { new OrderJsonConverter() }
            };
        }

        /// <summary>
        /// Check if Api is successfully connected with correct credentials
        /// </summary>
        public bool Connected
        {
            get
            {
                return ApiConnection.Connected;
            }
        }

        /// <summary>
        /// Create a project with the specified name and language via QuantConnect.com API
        /// </summary>
        /// <param name="name">Project name</param>
        /// <param name="language">Programming language to use</param>
        /// <returns>Project object from the API.</returns>

        public ProjectResponse CreateProject(string name, Language language)
        {
            var request = new RestRequest("projects/create", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                name, 
                language
            }), ParameterType.RequestBody);

            ProjectResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Get details about a single project
        /// </summary>
        /// <param name="projectId">Id of the project</param>
        /// <returns><see cref="ProjectResponse"/> that contains information regarding the project</returns>

        public ProjectResponse ReadProject(int projectId)
        {
            var request = new RestRequest("projects/read", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
                {
                    projectId
                }), ParameterType.RequestBody);

            ProjectResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// List details of all projects
        /// </summary>
        /// <returns><see cref="ProjectResponse"/> that contains information regarding the project</returns>

        public ProjectResponse ListProjects()
        {
            var request = new RestRequest("projects/read", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            ProjectResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }


        /// <summary>
        /// Add a file to a project
        /// </summary>
        /// <param name="projectId">The project to which the file should be added</param>
        /// <param name="name">The name of the new file</param>
        /// <param name="content">The content of the new file</param>
        /// <returns><see cref="ProjectFilesResponse"/> that includes information about the newly created file</returns>

        public ProjectFilesResponse AddProjectFile(int projectId, string name, string content)
        {
            var request = new RestRequest("files/create", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
                {
                    projectId,
                    name,
                    content
                }), ParameterType.RequestBody);

            ProjectFilesResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }


        /// <summary>
        /// Update the name of a file
        /// </summary>
        /// <param name="projectId">Project id to which the file belongs</param>
        /// <param name="oldFileName">The current name of the file</param>
        /// <param name="newFileName">The new name for the file</param>
        /// <returns><see cref="RestResponse"/> indicating success</returns>

        public RestResponse UpdateProjectFileName(int projectId, string oldFileName, string newFileName)
        {
            var request = new RestRequest("files/update", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
                {
                    projectId,
                    name = oldFileName,
                    newName = newFileName
                }), ParameterType.RequestBody);

            RestResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }


        /// <summary>
        /// Update the contents of a file
        /// </summary>
        /// <param name="projectId">Project id to which the file belongs</param>
        /// <param name="fileName">The name of the file that should be updated</param>
        /// <param name="newFileContents">The new contents of the file</param>
        /// <returns><see cref="RestResponse"/> indicating success</returns>

        public RestResponse UpdateProjectFileContent(int projectId, string fileName, string newFileContents)
        {
            var request = new RestRequest("files/update", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
                {
                    projectId,
                    name = fileName,
                    content = newFileContents
                }), ParameterType.RequestBody);

            RestResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }


        /// <summary>
        /// Read all files in a project
        /// </summary>
        /// <param name="projectId">Project id to which the file belongs</param>
        /// <returns><see cref="ProjectFilesResponse"/> that includes the information about all files in the project</returns>

        public ProjectFilesResponse ReadProjectFiles(int projectId)
        {
            var request = new RestRequest("files/read", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
                {
                    projectId
                }), ParameterType.RequestBody);

            ProjectFilesResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }


        /// <summary>
        /// Read a file in a project
        /// </summary>
        /// <param name="projectId">Project id to which the file belongs</param>
        /// <param name="fileName">The name of the file</param>
        /// <returns><see cref="ProjectFilesResponse"/> that includes the file information</returns>

        public ProjectFilesResponse ReadProjectFile(int projectId, string fileName)
        {
            var request = new RestRequest("files/read", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
                {
                    projectId,
                    name = fileName
                }), ParameterType.RequestBody);

            ProjectFilesResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Delete a file in a project
        /// </summary>
        /// <param name="projectId">Project id to which the file belongs</param>
        /// <param name="name">The name of the file that should be deleted</param>
        /// <returns><see cref="ProjectFilesResponse"/> that includes the information about all files in the project</returns>

        public RestResponse DeleteProjectFile(int projectId, string name)
        {
            var request = new RestRequest("files/delete", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
                {
                    projectId,
                    name,
                }), ParameterType.RequestBody);

            RestResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Delete a project
        /// </summary>
        /// <param name="projectId">Project id we own and wish to delete</param>
        /// <returns>RestResponse indicating success</returns>

        public RestResponse DeleteProject(int projectId)
        {
            var request = new RestRequest("projects/delete", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                projectId
            }), ParameterType.RequestBody);

            RestResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Create a new compile job request for this project id.
        /// </summary>
        /// <param name="projectId">Project id we wish to compile.</param>
        /// <returns>Compile object result</returns>

        public Compile CreateCompile(int projectId)
        {
            var request = new RestRequest("compile/create", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                projectId
            }), ParameterType.RequestBody);

            Compile result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Read a compile packet job result.
        /// </summary>
        /// <param name="projectId">Project id we sent for compile</param>
        /// <param name="compileId">Compile id return from the creation request</param>
        /// <returns><see cref="Compile"/></returns>

        public Compile ReadCompile(int projectId, string compileId)
        {
            var request = new RestRequest("compile/read", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                projectId,
                compileId
            }), ParameterType.RequestBody);

            Compile result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }


        /// <summary>
        /// Create a new backtest request and get the id.
        /// </summary>
        /// <param name="projectId">Id for the project to backtest</param>
        /// <param name="compileId">Compile id for the project</param>
        /// <param name="backtestName">Name for the new backtest</param>
        /// <returns><see cref="Backtest"/>t</returns>

        public Backtest CreateBacktest(int projectId, string compileId, string backtestName)
        {
            var request = new RestRequest("backtests/create", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                projectId,
                compileId,
                backtestName
            }), ParameterType.RequestBody);

            BacktestResponseWrapper result;
            ApiConnection.TryRequest(request, out result);

            // Use API Response values for Backtest Values
            result.Backtest.Success = result.Success;
            result.Backtest.Errors = result.Errors;

            // Return only the backtest object
            return result.Backtest;
        }

        /// <summary>
        /// Read out a backtest in the project id specified.
        /// </summary>
        /// <param name="projectId">Project id to read</param>
        /// <param name="backtestId">Specific backtest id to read</param>
        /// <param name="getCharts">True will return backtest charts</param>
        /// <returns><see cref="Backtest"/></returns>

        public Backtest ReadBacktest(int projectId, string backtestId, bool getCharts = true)
        {
            var request = new RestRequest("backtests/read", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                projectId,
                backtestId
            }), ParameterType.RequestBody);

            BacktestResponseWrapper result;
            ApiConnection.TryRequest(request, out result);

            if (!result.Success)
            {
                // place an empty place holder so we can return any errors back to the user and not just null
                result.Backtest = new Backtest { BacktestId = backtestId };
            }
            // Go fetch the charts if the backtest is completed and success
            else if (getCharts && result.Backtest.Completed)
            {
                // For storing our collected charts
                var updatedCharts = new Dictionary<string, Chart>();

                // Create backtest requests for each chart that is empty
                foreach (var chart in result.Backtest.Charts)
                {
                    if (!chart.Value.Series.IsNullOrEmpty())
                    {
                        continue;
                    }

                    var chartRequest = new RestRequest("backtests/read", Method.POST)
                    {
                        RequestFormat = DataFormat.Json
                    };

                    chartRequest.AddParameter("application/json", JsonConvert.SerializeObject(new
                    {
                        projectId,
                        backtestId,
                        chart = chart.Key.Replace(' ', '+')
                    }), ParameterType.RequestBody);

                    BacktestResponseWrapper chartResponse;
                    ApiConnection.TryRequest(chartRequest, out chartResponse);

                    // Add this chart to our updated collection
                    if (chartResponse.Success)
                    {
                        updatedCharts.Add(chart.Key, chartResponse.Backtest.Charts[chart.Key]);
                    }
                }

                // Update our result
                foreach(var updatedChart in updatedCharts)
                {
                    result.Backtest.Charts[updatedChart.Key] = updatedChart.Value;
                }
            }

            // Use API Response values for Backtest Values
            result.Backtest.Success = result.Success;
            result.Backtest.Errors = result.Errors;

            // Return only the backtest object
            return result.Backtest;
        }

        /// <summary>
        /// Update a backtest name
        /// </summary>
        /// <param name="projectId">Project for the backtest we want to update</param>
        /// <param name="backtestId">Backtest id we want to update</param>
        /// <param name="name">Name we'd like to assign to the backtest</param>
        /// <param name="note">Note attached to the backtest</param>
        /// <returns><see cref="RestResponse"/></returns>

        public RestResponse UpdateBacktest(int projectId, string backtestId, string name = "", string note = "")
        {
            var request = new RestRequest("backtests/update", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                projectId,
                backtestId,
                name,
                note
            }), ParameterType.RequestBody);

            Backtest result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// List all the backtests for a project
        /// </summary>
        /// <param name="projectId">Project id we'd like to get a list of backtest for</param>
        /// <returns><see cref="BacktestList"/></returns>

        public BacktestList ListBacktests(int projectId)
        {
            var request = new RestRequest("backtests/read", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                projectId,
            }), ParameterType.RequestBody);

            BacktestList result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Delete a backtest from the specified project and backtestId.
        /// </summary>
        /// <param name="projectId">Project for the backtest we want to delete</param>
        /// <param name="backtestId">Backtest id we want to delete</param>
        /// <returns><see cref="RestResponse"/></returns>

        public RestResponse DeleteBacktest(int projectId, string backtestId)
        {
            var request = new RestRequest("backtests/delete", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                projectId,
                backtestId
            }), ParameterType.RequestBody);

            RestResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Create a live algorithm.
        /// </summary>
        /// <param name="projectId">Id of the project on QuantConnect</param>
        /// <param name="compileId">Id of the compilation on QuantConnect</param>
        /// <param name="nodeId">Id of the node that will run the algorithm</param>
        /// <param name="baseLiveAlgorithmSettings">Brokerage specific <see cref="BaseLiveAlgorithmSettings">BaseLiveAlgorithmSettings</see>.</param>
        /// <param name="versionId">The version of the Lean used to run the algorithm.
        ///                         -1 is master, however, sometimes this can create problems with live deployments.
        ///                         If you experience problems using, try specifying the version of Lean you would like to use.</param>
        /// <returns>Information regarding the new algorithm <see cref="LiveAlgorithm"/></returns>

        public LiveAlgorithm CreateLiveAlgorithm(int projectId,
                                                 string compileId,
                                                 string nodeId,
                                                 BaseLiveAlgorithmSettings baseLiveAlgorithmSettings,
                                                 string versionId = "-1")
        {
            var request = new RestRequest("live/create", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(
                new LiveAlgorithmApiSettingsWrapper
                (projectId,
                compileId,
                nodeId,
                baseLiveAlgorithmSettings,
                versionId)
                ), ParameterType.RequestBody);

            LiveAlgorithm result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Get a list of live running algorithms for user
        /// </summary>
        /// <param name="status">Filter the statuses of the algorithms returned from the api</param>
        /// <param name="startTime">Earliest launched time of the algorithms returned by the Api</param>
        /// <param name="endTime">Latest launched time of the algorithms returned by the Api</param>
        /// <returns><see cref="LiveList"/></returns>

        public LiveList ListLiveAlgorithms(AlgorithmStatus? status = null,
                                           DateTime? startTime = null,
                                           DateTime? endTime = null)
        {
            // Only the following statuses are supported by the Api
            if (status.HasValue                        &&
                status != AlgorithmStatus.Running      &&
                status != AlgorithmStatus.RuntimeError &&
                status != AlgorithmStatus.Stopped      &&
                status != AlgorithmStatus.Liquidated)
            {
                throw new ArgumentException(
                    "The Api only supports Algorithm Statuses of Running, Stopped, RuntimeError and Liquidated");
            }

            var request = new RestRequest("live/read", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            var epochStartTime = startTime == null ? 0 : Time.DateTimeToUnixTimeStamp(startTime.Value);
            var epochEndTime   = endTime   == null ? Time.DateTimeToUnixTimeStamp(DateTime.UtcNow) : Time.DateTimeToUnixTimeStamp(endTime.Value);

            JObject obj = new JObject
            {
                { "start", epochStartTime },
                { "end", epochEndTime }
            };

            if (status.HasValue)
            {
                obj.Add("status", status.ToString());
            }

            request.AddParameter("application/json", JsonConvert.SerializeObject(obj), ParameterType.RequestBody);

            LiveList result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Read out a live algorithm in the project id specified.
        /// </summary>
        /// <param name="projectId">Project id to read</param>
        /// <param name="deployId">Specific instance id to read</param>
        /// <returns><see cref="LiveAlgorithmResults"/></returns>

        public LiveAlgorithmResults ReadLiveAlgorithm(int projectId, string deployId)
        {
            var request = new RestRequest("live/read", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
                {
                    projectId,
                    deployId
                }), ParameterType.RequestBody);

            LiveAlgorithmResults result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Liquidate a live algorithm from the specified project and deployId.
        /// </summary>
        /// <param name="projectId">Project for the live instance we want to stop</param>
        /// <returns><see cref="RestResponse"/></returns>

        public RestResponse LiquidateLiveAlgorithm(int projectId)
        {
            var request = new RestRequest("live/update/liquidate", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
                {
                    projectId
                }), ParameterType.RequestBody);

            RestResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Stop a live algorithm from the specified project and deployId.
        /// </summary>
        /// <param name="projectId">Project for the live instance we want to stop</param>
        /// <returns><see cref="RestResponse"/></returns>

        public RestResponse StopLiveAlgorithm(int projectId)
        {
            var request = new RestRequest("live/update/stop", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                projectId
            }), ParameterType.RequestBody);

            RestResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Gets the logs of a specific live algorithm
        /// </summary>
        /// <param name="projectId">Project Id of the live running algorithm</param>
        /// <param name="algorithmId">Algorithm Id of the live running algorithm</param>
        /// <param name="startTime">No logs will be returned before this time</param>
        /// <param name="endTime">No logs will be returned after this time</param>
        /// <returns><see cref="LiveLog"/> List of strings that represent the logs of the algorithm</returns>

        public LiveLog ReadLiveLogs(int projectId, string algorithmId, DateTime? startTime = null, DateTime? endTime = null)
        {
            var epochStartTime = startTime == null ? 0 : Time.DateTimeToUnixTimeStamp(startTime.Value);
            var epochEndTime   = endTime   == null ? Time.DateTimeToUnixTimeStamp(DateTime.UtcNow) : Time.DateTimeToUnixTimeStamp(endTime.Value);

            var request = new RestRequest("live/read/log", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                format = "json",
                projectId,
                algorithmId,
                start = epochStartTime,
                end = epochEndTime
            }), ParameterType.RequestBody);

            LiveLog result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Gets the link to the downloadable data.
        /// </summary>
        /// <param name="symbol">Symbol of security of which data will be requested.</param>
        /// <param name="resolution">Resolution of data requested.</param>
        /// <param name="date">Date of the data requested.</param>
        /// <returns><see cref="Link"/> to the downloadable data.</returns>

        public Link ReadDataLink(Symbol symbol, Resolution resolution, DateTime date)
        {
            var request = new RestRequest("data/read", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                format = "link",
                ticker = symbol.Value.ToLowerInvariant(),
                type = symbol.ID.SecurityType.ToLower(),
                market = symbol.ID.Market,
                resolution = resolution.ToString(),
                date = date.ToStringInvariant("yyyyMMdd")
            }), ParameterType.RequestBody);

            Link result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Read out the report of a backtest in the project id specified.
        /// </summary>
        /// <param name="projectId">Project id to read</param>
        /// <param name="backtestId">Specific backtest id to read</param>
        /// <returns><see cref="BacktestReport"/></returns>
        public BacktestReport ReadBacktestReport(int projectId, string backtestId)
        {
            var request = new RestRequest("backtests/read/report", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                backtestId,
                projectId
            }), ParameterType.RequestBody);

            BacktestReport report;
            ApiConnection.TryRequest(request, out report);
            return report;
        }

        /// <summary>
        /// Method to download and save the data purchased through QuantConnect
        /// </summary>
        /// <param name="symbol">Symbol of security of which data will be requested.</param>
        /// <param name="resolution">Resolution of data requested.</param>
        /// <param name="date">Date of the data requested.</param>
        /// <returns>A <see cref="bool"/> indicating whether the data was successfully downloaded or not.</returns>

        public bool DownloadData(Symbol symbol, Resolution resolution, DateTime date)
        {
            // Get a link to the data
            var link = ReadDataLink(symbol, resolution, date);

            // Make sure the link was successfully retrieved
            if (!link.Success)
                return false;

            // Save csv in same folder heirarchy as Lean
            var path = Path.Combine(_dataFolder, LeanData.GenerateRelativeZipFilePath(symbol.Value, symbol.ID.SecurityType, symbol.ID.Market, date, resolution));

            // Make sure the directory exist before writing
            (new FileInfo(path)).Directory.Create();

            // Download and save the data
            var uri     = new Uri(link.DataLink);
            var client  = new RestClient(uri.Scheme + "://" + uri.Host);
            var request = new RestRequest(uri.PathAndQuery, Method.GET);

            // MAke a request for the data at the link
            var response = client.Execute(request);

            // If the response is JSON it doesn't contain any data, try and extract the message and write it
            if (response.ContentType.ToLowerInvariant() == "application/json")
            {
                try
                {
                    var contentObj = JObject.Parse(response.Content);
                    var message = contentObj["message"].Value<string>();
                    Log.Error($"Api.DownloadData(): Failed to download zip for {symbol} {resolution} data for date {date}, Api response: {message}");
                }
                catch
                {
                    Log.Error($"Api.DownloadData(): Failed to download zip for {symbol} {resolution} data for date {date}. Api response could not be parsed.");
                }

                return false;
            }
            
            // Any other case save the content to given path
            response.RawBytes.SaveAs(path);
            return true;
        }

        /// <summary>
        /// Get the algorithm status from the user with this algorithm id.
        /// </summary>
        /// <param name="algorithmId">String algorithm id we're searching for.</param>
        /// <returns>Algorithm status enum</returns>

        public virtual AlgorithmControl GetAlgorithmStatus(string algorithmId)
        {
            return new AlgorithmControl()
            {
                ChartSubscription = "*"
            };
        }

        /// <summary>
        /// Algorithm passes back its current status to the UX.
        /// </summary>
        /// <param name="status">Status of the current algorithm</param>
        /// <param name="algorithmId">String algorithm id we're setting.</param>
        /// <param name="message">Message for the algorithm status event</param>
        /// <returns>Algorithm status enum</returns>

        public virtual void SetAlgorithmStatus(string algorithmId, AlgorithmStatus status, string message = "")
        {
            //
        }

        /// <summary>
        /// Send the statistics to storage for performance tracking.
        /// </summary>
        /// <param name="algorithmId">Identifier for algorithm</param>
        /// <param name="unrealized">Unrealized gainloss</param>
        /// <param name="fees">Total fees</param>
        /// <param name="netProfit">Net profi</param>
        /// <param name="holdings">Algorithm holdings</param>
        /// <param name="equity">Total equity</param>
        /// <param name="netReturn">Net return for the deployment</param>
        /// <param name="volume">Volume traded</param>
        /// <param name="trades">Total trades since inception</param>
        /// <param name="sharpe">Sharpe ratio since inception</param>

        public virtual void SendStatistics(string algorithmId, decimal unrealized, decimal fees, decimal netProfit, decimal holdings, decimal equity, decimal netReturn, decimal volume, int trades, double sharpe)
        {
            //
        }

        /// <summary>
        /// Send an email to the user associated with the specified algorithm id
        /// </summary>
        /// <param name="algorithmId">The algorithm id</param>
        /// <param name="subject">The email subject</param>
        /// <param name="body">The email message body</param>

        public virtual void SendUserEmail(string algorithmId, string subject, string body)
        {
            //
        }

        /// <summary>
        /// Local implementation for downloading data to algorithms
        /// </summary>
        /// <param name="address">URL to download</param>
        /// <param name="headers">KVP headers</param>
        /// <param name="userName">Username for basic authentication</param>
        /// <param name="password">Password for basic authentication</param>
        /// <returns></returns>
        public virtual string Download(string address, IEnumerable<KeyValuePair<string, string>> headers, string userName, string password)
        {
            using (var client = new WebClient { Credentials = new NetworkCredential(userName, password) })
            {
                client.Proxy = WebRequest.GetSystemWebProxy();
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        client.Headers.Add(header.Key, header.Value);
                    }
                }
                // Add a user agent header in case the requested URI contains a query.
                client.Headers.Add("user-agent", "QCAlgorithm.Download(): User Agent Header");

                try
                {
                    return client.DownloadString(address);
                }
                catch (WebException exception)
                {
                    var message = $"Api.Download(): Failed to download data from {address}";
                    if (!userName.IsNullOrEmpty() || !password.IsNullOrEmpty())
                    {
                        message += $" with username: {userName} and password {password}";
                    }

                    throw new WebException($"{message}. Please verify the source for missing http:// or https://", exception);
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public virtual void Dispose()
        {
            // NOP
        }

        /// <summary>
        /// Generate a secure hash for the authorization headers.
        /// </summary>
        /// <returns>Time based hash of user token and timestamp.</returns>
        public static string CreateSecureHash(int timestamp, string token)
        {
            // Create a new hash using current UTC timestamp.
            // Hash must be generated fresh each time.
            var data = $"{token}:{timestamp.ToStringInvariant()}";
            return data.ToSHA256();
        }

        /// <summary>
        /// Create a new node in the organization, node configuration is defined by the
        /// <see cref="SKU"/>
        /// </summary>
        /// <param name="name">The name of the new node</param>
        /// <param name="organizationId">ID of the organization</param>
        /// <param name="sku"><see cref="SKU"/> Object representing configuration</param>
        /// <returns>Returns <see cref="CreatedNode"/> which contains API response and
        /// <see cref="Node"/></returns>
        public CreatedNode CreateNode(string name, string organizationId, SKU sku)
        {
            var request = new RestRequest("nodes/create", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                name,
                organizationId,
                sku = sku.ToString()
            }), ParameterType.RequestBody);

            CreatedNode result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Reads the nodes associated with the organization, creating a
        /// <see cref="NodeList"/> for the response
        /// </summary>
        /// <param name="organizationId">ID of the organization</param>
        /// <returns><see cref="NodeList"/> containing Backtest, Research, and Live Nodes</returns>
        public NodeList ReadNodes(string organizationId)
        {
            var request = new RestRequest("nodes/read", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                organizationId,
            }), ParameterType.RequestBody);

            NodeList result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Update an organizations node with a new name
        /// </summary>
        /// <param name="nodeId">The node ID of the node you want to update</param>
        /// <param name="newName">The new name for that node</param>
        /// <param name="organizationId">ID of the organization</param>
        /// <returns><see cref="RestResponse"/> containing success response and errors</returns>
        public RestResponse UpdateNode(string nodeId, string newName, string organizationId)
        {
            var request = new RestRequest("nodes/update", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                nodeId,
                name = newName,
                organizationId
            }), ParameterType.RequestBody);

            RestResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Delete a node from an organization, requires node ID.
        /// </summary>
        /// <param name="nodeId">The node ID of the node you want to delete</param>
        /// <param name="organizationId">ID of the organization</param>
        /// <returns><see cref="RestResponse"/> containing success response and errors</returns>
        public RestResponse DeleteNode(string nodeId, string organizationId)
        {
            var request = new RestRequest("nodes/delete", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                nodeId,
                organizationId
            }), ParameterType.RequestBody);

            RestResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Stop a running node in a organization
        /// </summary>
        /// <param name="nodeId">The node ID of the node you want to stop</param>
        /// <param name="organizationId">ID of the organization</param>
        /// <returns><see cref="RestResponse"/> containing success response and errors</returns>
        public RestResponse StopNode(string nodeId, string organizationId)
        {
            var request = new RestRequest("nodes/stop", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddParameter("application/json", JsonConvert.SerializeObject(new
            {
                nodeId,
                organizationId
            }), ParameterType.RequestBody);

            RestResponse result;
            ApiConnection.TryRequest(request, out result);
            return result;
        }

        /// <summary>
        /// Will read the organization account status
        /// </summary>
        /// <param name="organizationId">The target organization id, if null will return default organization</param>
        public Account ReadAccount(string organizationId = null)
        {
            var request = new RestRequest("account/read/", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            if (organizationId != null)
            {
                request.AddParameter("application/json", JsonConvert.SerializeObject(new { organizationId }), ParameterType.RequestBody);
            }

            Account account;
            ApiConnection.TryRequest(request, out account);
            return account;
        }
    }
}