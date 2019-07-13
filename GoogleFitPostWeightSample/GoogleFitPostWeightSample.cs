using Google.Apis.Auth.OAuth2;
using Google.Apis.Fitness.v1;
using Google.Apis.Fitness.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace GoogleFitTest
{
    public class GoogleFitPostWeightSample
    {
        private static readonly DateTime unixEpochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const string userId = "me";
        private const string dataTypeName = "com.google.weight";

        /// <summary>
        /// コンフィグ
        /// </summary>
        /// <returns></returns>
        static IConfiguration GetConfiguration()
        {
            var configBuilder = new ConfigurationBuilder();

            configBuilder.SetBasePath(Directory.GetCurrentDirectory());

            configBuilder.AddJsonFile(@"appsettings.json");

            return configBuilder.Build();
        }

        /// <summary>
        /// メイン
        /// </summary>
        /// <returns></returns>
        static async Task Main()
        {
            var config = GetConfiguration();

            await postGoogleFit(config);
        }

        /// <summary>
        /// UNIX時間でのナノ秒値
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static long GetUnixEpochNanoSeconds(DateTime dt)
        {
            return (dt.Ticks - unixEpochStart.Ticks) * 100;
        }

        /// <summary>
        /// GoogleFit投稿処理
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static async Task postGoogleFit(IConfiguration config)
        {

            UserCredential credential;
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                new ClientSecrets
                {
                    ClientId = config.GetSection("AppSettings")["ClientId"],
                    ClientSecret = config.GetSection("AppSettings")["ClientSecret"]
                },
                new string[]
                {
                    FitnessService.Scope.FitnessBodyRead,
                    FitnessService.Scope.FitnessBodyWrite
                },
                "user",
                CancellationToken.None,
                new FileDataStore("GoogleFitnessAuth", true)//trueにするとカレントパスに保存
                );

            var fitnessService = new FitnessService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = Assembly.GetExecutingAssembly().GetName().Name
            });

            var dataSource = new DataSource()
            {
                Type = "derived",
                DataStreamName = "GoogieFitTestDataSource",
                Application = new Application()
                {
                    Name = "TanitaHealthPlanet",
                    Version = "1"
                },
                DataType = new DataType()
                {
                    Name = dataTypeName,
                    Field = new List<DataTypeField>()
                    {
                        new DataTypeField() {Name = "weight", Format = "floatPoint"}
                    }
                },
                Device = new Device()
                {
                    Manufacturer = "Tanita",
                    Model = "RD-906",
                    Type = "scale",
                    Uid = "1000001",
                    Version = "1.0"
                }
            };

            var dataSourceId = $"{dataSource.Type}:{dataSource.DataType.Name}:{config.GetSection("AppSettings")["ClientId"].Split('-')[0]}:{dataSource.Device.Manufacturer}:{dataSource.Device.Model}:{dataSource.Device.Uid}:{dataSource.DataStreamName}";

            var dataSrcList = await fitnessService.Users.DataSources.List(userId).ExecuteAsync();

            if (dataSrcList.DataSource.Select(s => s.DataStreamId).Any(s => s == dataSourceId))
            {
                dataSource = fitnessService.Users.DataSources.Get(userId, dataSourceId).Execute();
            }
            else
            {
                dataSource = fitnessService.Users.DataSources.Create(dataSource, userId).Execute();
            }

            var postNanosec = GetUnixEpochNanoSeconds(DateTime.UtcNow);
            var widthDataSource = new Dataset()
            {
                DataSourceId = dataSourceId,
                MaxEndTimeNs = postNanosec,
                MinStartTimeNs = postNanosec,
                Point = new List<DataPoint>()
                {
                    new DataPoint()
                    {
                        DataTypeName = dataTypeName,
                        StartTimeNanos = postNanosec,
                        EndTimeNanos = postNanosec,
                        Value = new List<Value>()
                        {
                            new Value()
                            {
                                FpVal = 80.0
                            }
                        }
                    }
                }
            };

            var dataSetId = $"{postNanosec}-{postNanosec}";
            await fitnessService.Users.DataSources.Datasets.Patch(widthDataSource, userId, dataSourceId, dataSetId).ExecuteAsync();
        }
    }
}
