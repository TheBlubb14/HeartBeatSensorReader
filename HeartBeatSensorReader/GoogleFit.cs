using Google.Apis.Fitness.v1.Data;
using Google.Apis.Fitness.v1;
using Google.Apis.Services;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace HeartBeatSensorReader;

internal class GoogleFit
{
    public static bool deleteAll = false;
    private static UserCredential credential;
    const string DataStreamId = "raw:com.google.heart_rate.bpm:248114505875";
    const string DataStreamIdActivity = "derived:com.google.activity.segment:248114505875";

    public static async Task UploadFromFiles(string path, FitnessService? service = null)
    {
        if (service is null)
            service = await GetFitnessService();

        await CreateDefaultDataSources(service);

        if (deleteAll)
        {

            var sessionToDelete = await service.Users.Sessions.List("me").ExecuteAsync();
            foreach (var item in sessionToDelete.Session)
            {
                if (item.Name == "Beatsaber VR Workout")
                    try
                    {
                        var sessionDelete = await service.Users.Sessions.Delete("me", item.Id).ExecuteAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
            }
        }

        var pointsExisting = Directory.GetFiles(path, "BPMoverTime_*.txt", SearchOption.TopDirectoryOnly);
        var regex = new Regex(@"(\d{2}\.\d{2}\.\d{4}\s\d{2}:\d{2}:\d{2}): (\d+)");


        await UploadFiles(pointsExisting, regex, "dd.MM.yyyy HH:mm:ss");
        pointsExisting = Directory.GetFiles(path, "BPMoverTimeNew_*.txt", SearchOption.TopDirectoryOnly);
        regex = new Regex(@"(\d{2}\.\d{2}\.\d{4}\s\d{2}:\d{2}:\d{2}\.\d{4}): (\d+)");

        await UploadFiles(pointsExisting, regex, "dd.MM.yyyy HH:mm:ss.ffff");

    }

    private static async Task UploadFiles(string[] pointsExisting, Regex regex, [StringSyntax(StringSyntaxAttribute.DateTimeFormat)] string dateFormat)
    {
        List<BpmData> datas = new();
        foreach (var file in pointsExisting)
        {
            if (file.Contains("not a number"))
            {
                continue;
            }

            foreach (var line in File.ReadAllLines(file))
            {
                Match match = regex.Match(line);
                if (match.Success)
                {
                    string datetimeString = match.Groups[1].Value;
                    string heartrateString = match.Groups[2].Value;


                    DateTime datetime = DateTime.ParseExact(datetimeString, dateFormat, null);
                    int heartrate = int.Parse(heartrateString);
                    datas.Add(new(datetime, heartrate, GetNanoSecondsSinceEpochFrom(datetime)));
                }
                else
                {
                    Console.WriteLine("No match found");
                }

            }


            await UploadData(datas);
            var fi = new FileInfo(file);
            File.Move(fi.FullName, Path.Combine(fi.Directory.FullName, "BPMDone", fi.Name));
            Console.WriteLine($"Processed {file} with {datas.Count} datas");
            datas.Clear();

        }
    }

    public static async Task UploadData(List<BpmData> datas, bool deleteBefore = false, int activityType = 108)
    {

        using FitnessService service = await GetFitnessService();
        if ((credential.Token.IssuedUtc.AddSeconds(credential.Token.ExpiresInSeconds.Value) - DateTime.UtcNow).TotalSeconds < 120)
            await credential.RefreshTokenAsync(CancellationToken.None);
        await UploadData(service, datas, deleteBefore, activityType);
    }
    public static async Task UploadData(FitnessService service, List<BpmData> datas, bool deleteBefore = false, int activityType = 108)
    {
        datas = datas.OrderBy(x => x.NanoSecondsSinceEpoch).ToList();
        SortAndUniquifyDatas(datas);

        var minData = datas.First();
        var maxData = datas.Last();
        var startMs = GetMilliSecondsSinceEpochFrom(minData.Occured);
        var endMs = GetMilliSecondsSinceEpochFrom(maxData.Occured);
        var sessionId = $"{(activityType == 9 ? "Aeorobic" : "Beatsaber")}_{startMs}-{endMs}";

        var dataset = new Dataset
        {
            DataSourceId = DataStreamId,
            MinStartTimeNs = GetNanoSecondsSinceEpochFrom(minData.Occured),
            MaxEndTimeNs = GetNanoSecondsSinceEpochFrom(maxData.Occured),
        };

        var dataSetId = $"{minData.NanoSecondsSinceEpoch}-{maxData.NanoSecondsSinceEpoch}";
        if (deleteBefore)
        {

            var deletion = await service.Users.DataSources.Datasets.Delete("me", DataStreamId, dataSetId).ExecuteAsync();
        }


        for (int i = 0; i <= datas.Count / 1000; i++)
        {
            dataset.Point = datas
                .Skip(i * 1000)
                .Take(1000)
                .Select(x => new DataPoint
                {
                    DataTypeName = "com.google.heart_rate.bpm",
                    StartTimeNanos = x.NanoSecondsSinceEpoch,
                    EndTimeNanos = x.NanoSecondsSinceEpoch,
                    Value = new List<Value> { new Value { FpVal = x.Heartrate } },

                })
                .ToList();


            var res = await service.Users.DataSources.Datasets.Patch(dataset, "me", DataStreamId, dataSetId).ExecuteAsync();
        }


        var session = new Session()
        {
            StartTimeMillis = startMs,
            EndTimeMillis = endMs,
            ActiveTimeMillis = endMs - startMs,
            ActivityType = activityType, //Andere (nicht klassifizierte Fitnessaktivitäten)
            Application = new Application
            {
                Name = "Heart Beat Sensor Reader",
                Version = "1.0"
            },
            Description = activityType  == 9 ? "Aeorobic" : "Beatsaber VR",
            Name = activityType == 9 ? "Aeorobic Training" : "Beatsaber VR Workout",
            Id = sessionId
        };
        session = await service.Users.Sessions.Update(session, "me", sessionId).ExecuteAsync();



        var datasetActivity = new Dataset
        {
            DataSourceId = DataStreamIdActivity,
            MinStartTimeNs = GetNanoSecondsSinceEpochFrom(minData.Occured),
            MaxEndTimeNs = GetNanoSecondsSinceEpochFrom(maxData.Occured),
        };


        datasetActivity.Point = new List<DataPoint>() {
                    new DataPoint
                   {
                       DataTypeName = "com.google.activity.segment",
                       StartTimeNanos = minData.NanoSecondsSinceEpoch,
                       EndTimeNanos = maxData.NanoSecondsSinceEpoch,
                       Value = new List<Value>
                            {
                                new Value
                                {
                                    IntVal = 108, //Andere (nicht klassifizierte Fitnessaktivitäten)
                                }
                            }
                   }
                };


        var createdActivityDataset = await service.Users.DataSources.Datasets.Patch(datasetActivity, "me", DataStreamIdActivity, dataSetId).ExecuteAsync();


        Console.WriteLine($"Inserted {datas.Count}");
    }

    private static async Task CreateDefaultDataSources(FitnessService service)
    {
        DataSource dataSource = await GetDataSource(service, DataStreamId, "com.google.heart_rate.bpm",
            new DataTypeField { Name = "bpm", Format = "floatingPoint" }, "raw");

        DataSource activitySegmentDataSource = await GetDataSource(service, DataStreamIdActivity, "com.google.activity.segment", new DataTypeField { Name = "activity", Format = "integer" }, "derived");
    }

    internal static async Task<FitnessService> GetFitnessService()
    {
        FitnessService service;
        try
        {

            await AuthorizeToGoogle();
            service = new FitnessService(new BaseClientService.Initializer()
            {
                ApplicationName = "Heart Beat Sensor Reader",
                HttpClientInitializer = credential,

            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            service = await GetFitnessService();
        }
        return service;
    }

    private static async Task AuthorizeToGoogle()
    {
        if (credential is not null)
            return;
        using var stream = File.OpenRead("additionalfiles/client_secret.json");

        credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(

                new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = GoogleClientSecrets.FromStream(stream).Secrets,


                },
            new string[]
            {
                    FitnessService.ScopeConstants.FitnessHeartRateWrite,
                    FitnessService.ScopeConstants.FitnessHeartRateRead,
                    FitnessService.ScopeConstants.FitnessActivityRead,
                    FitnessService.ScopeConstants.FitnessActivityWrite,
                    FitnessService.ScopeConstants.FitnessBodyRead,
                    FitnessService.ScopeConstants.FitnessBodyWrite,
            },
            "Sascha",
            CancellationToken.None,
            new FileDataStore("GoogleFitnessAuth", true)
            );

        var res = await credential.RefreshTokenAsync(CancellationToken.None);

    }
    private static async Task<DataSource> GetDataSource(FitnessService service, string DataStreamId, string dataTypeName, DataTypeField field, string type)
    {
        DataSource dataSource = null;
        try
        {
            dataSource = await service.Users.DataSources.Get("me", DataStreamId).ExecuteAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        if (dataSource is null)
        {

            dataSource = new DataSource
            {
                Type = type,
                DataStreamId = DataStreamId,
                DataType = new DataType
                {
                    Name = dataTypeName,
                    Field = new List<DataTypeField>
            {
                            field
            }
                },
                Application = new Application
                {
                    Name = "Heart Beat Sensor Reader",
                    Version = "1.0"
                }
            };
            var dataSourceRequest = service.Users.DataSources.Create(dataSource, "me");
            dataSource = await dataSourceRequest.ExecuteAsync();

        }

        return dataSource;
    }



    private static void SortAndUniquifyDatas(List<BpmData> datas)
    {
        BpmData previous = datas[0];
        for (int i = 1; i < datas.Count - 1; i++)
        {
            var current = datas[i];
            if (current.Occured == previous.Occured)
            {
                var newDate = previous.Occured.AddSeconds(-1);
                datas[i - 1] = new BpmData(newDate, previous.Heartrate, GetNanoSecondsSinceEpochFrom(newDate));
                i -= 3;

                if (i <= 1)
                {
                    previous = datas[0];
                    i = 0;
                }
                else
                {
                    previous = datas[i - 1];
                }
            }
            else
                previous = current;
        }
    }

    public static long GetNanoSecondsSinceEpochFrom(DateTime dt)
    {
        return new DateTimeOffset(dt).ToUnixTimeMilliseconds() * 1000000;
    }

    public static long GetMilliSecondsSinceEpochFrom(DateTime dt)
    {
        return new DateTimeOffset(dt).ToUnixTimeMilliseconds();
    }

}
