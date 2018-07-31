using System;
using System.Text;
using System.IO;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Sharp7;
using System.Net.Sockets;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using ClientDemo.Config;
using Newtonsoft.Json;
using System.Threading.Tasks;
using LiteDB;
using Windows.Storage;
using ClientDemo.Process;


namespace ClientDemo
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        static S7Client Client;
        public static MqttClient MQClient;
        static Config.Application GWApp;
        static GWConfig GWConfig;
        static GWConfigUpdate GWConfigUpdate;
        static bool GWisBusy;
        static bool initialConfig;
        static string configJson;
       
        

        public MainPage()
        {
            this.InitializeComponent();
            this.InitializeGW();
            initialConfig = true;
        }


        private void ShowResult(int Result)
        {
            // This function returns a textual explaination of the error code
            //TxtResult.Text = Client.ErrorText(Result);
        }

        private void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            //int Result = Client.ConnectTo(TxtIP.Text, Convert.ToInt32(TxtRack.Text), Convert.ToInt32(TxtSlot.Text));
            //ShowResult(Result);
            //if (Result == 0)
            //    RunMode();
        }

        private void DisconnectBtn_Click(object sender, RoutedEventArgs e)
        {
            int Result = Client.Disconnect();
            ShowResult(Result);
           // BrowseMode();
        }

        private void ReadBtn_Click(object sender, RoutedEventArgs e)
        {
            //int Size = Convert.ToInt32(TxtSize.Text);
            //int Result = Client.DBRead(Convert.ToInt32(TxtDB.Text), Convert.ToInt32(TxtStart.Text), Size, Buffer);
            //ShowResult(Result);
            //if (Result == 0)
            //    HexDump(Buffer, Size);
        }

        private void InitializeGW()
        {
            loadJSONConfig();

            GWApp = JsonConvert.DeserializeObject<Config.Application>(configJson);

            Device me = new Device();

            //connect to MQTT
            MQClient = new MqttClient(GWApp.MQTTSettings.Broker);
            MQClient.MqttMsgPublishReceived += MQClient_recievedMessage;
            string clientId = Guid.NewGuid().ToString();
            MQClient.Connect(clientId);

            MQClient.Publish(GWApp.DeviceID + GWApp.MQTTSettings.StatusTopic, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(me)));
          
            //subscribe to get configs and update
            MQClient.Subscribe(new String[] { (GWApp.DeviceID + GWApp.MQTTSettings.ConfigTopic), (GWApp.DeviceID + GWApp.MQTTSettings.UpdateTopic) }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });

        }

        static void MQClient_recievedMessage(object sender, MqttMsgPublishEventArgs e)
        {
            if (!GWisBusy)
            {
                GWisBusy = true;
                var message = Encoding.UTF8.GetString(e.Message);
                MQClient.Publish(GWApp.DeviceID + GWApp.MQTTSettings.StatusTopic, Encoding.UTF8.GetBytes(GWApp.MQTTSettings.Messages.WorkingOnConfig));


                if (e.Topic == GWApp.DeviceID + GWApp.MQTTSettings.ConfigTopic)
                {
                    try
                    {

                        GWConfig = JsonConvert.DeserializeObject<GWConfig>(message);

                        //LiteDatabase db = new LiteDatabase(@"C:\Temp\GW_Data.db");

                        //    File.SetAttributes(@"C:\Temp\GW_Data.db", FileAttributes.Normal);
                        //    var col = db.GetCollection<GWConfig>("Config");
                        //    col.Insert(GWConfig);


                        if (!GWConfig.VarOK())
                        {
                            MQClient.Publish(GWApp.DeviceID + GWApp.MQTTSettings.StatusTopic, Encoding.UTF8.GetBytes("Cannot update. Invalid format"));
                        }

                        //if (!(ConnectToPLC(GWConfig.PLC) == 0))
                        //{
                        //    MQClient.Publish("HSMA/IOTGW/status", Encoding.UTF8.GetBytes("something is wrong with the plc"));
                        //    GWisBusy = false;
                        //    return;
                        //}


                        GWisBusy = false;
                        initialConfig = false;
                        GWConfig.InitTimer();
                    }
                    catch
                    {
                        MQClient.Publish(GWApp.DeviceID + GWApp.MQTTSettings.StatusTopic, Encoding.UTF8.GetBytes("Cannot update. Invalid format"));
                        GWisBusy = false;
                    }
                }
                else
                {
                    try
                    {
                        GWConfigUpdate = JsonConvert.DeserializeObject<GWConfigUpdate>(message);

                        if (initialConfig)
                        {

                            MQClient.Publish(GWApp.DeviceID + GWApp.MQTTSettings.StatusTopic, Encoding.UTF8.GetBytes("Cannot update if no config avaliable"));
                        }
                        else if (GWConfig.configID == GWConfigUpdate.ConfigID)
                        {
                            GWConfig.CancelTimer();
                            GWConfig.interval = GWConfigUpdate.interval;
                            GWConfig.InitTimer();
                        }
                        else
                        {
                            MQClient.Publish(GWApp.DeviceID + GWApp.MQTTSettings.StatusTopic, Encoding.UTF8.GetBytes("Cannot update. ID is not correct "));
                        }
                    }
                    catch
                    {
                        MQClient.Publish(GWApp.DeviceID + GWApp.MQTTSettings.StatusTopic, Encoding.UTF8.GetBytes("Cannot update. Invalid format"));
                    }

                    GWisBusy = false;
                }
               
            }

           

        }
        
        static int ConnectToPLC(PLC inputPLC)
        {
            return Client.ConnectTo(inputPLC.IPAddress, 1, 1);
           
        }

        public async void loadJSONConfig()
        {
            string  ConfigFile = @"Properties\ApplicationConfig.json";
            var folder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            var file = await folder.GetFileAsync(ConfigFile);
            var read = await FileIO.ReadTextAsync(file);
            configJson = read;            

        }


        //static void readAndSend(Variable[] inputVars)
        //{
        //    int i = 0;


        //    while (GWisBusy)
        //    {
        //        for (i = 0; i < inputVars.Length; i++)
        //        {
        //            //int Result = Client.DBRead(inputVars[i].DBNr, inputVars[i].Offset, 11, new byte[] { });
        //            //inputVars[i].Value = HexDump();
        //            MQClient.Publish(GWApp.MQTTSettings.ReadingsTopic, Encoding.UTF8.GetBytes(inputVars[i].Name +"': '"+ inputVars[i].Value +"'"));
        //        }
        //    }
        //}

    }
}
