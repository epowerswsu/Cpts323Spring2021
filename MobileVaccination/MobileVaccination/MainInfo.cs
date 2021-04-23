using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MobileVaccination.ClassDefinitions;
using Firebase.Database;
using Firebase.Database.Query;
using System.Threading;

namespace MobileVaccination
{
    public partial class MainInfo : Form
    {
        private static List<Van> vanList = new List<Van>();
        private static int currentVanIndex;
        public static FirebaseClient client;
        private static Mutex mutex = new Mutex();

        public MainInfo()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Hide();
            this.Close();
        }
        private void button2_Click(object sender, EventArgs e)
        {
            //switch to the next van in the vanList
            currentVanIndex++;
            if(currentVanIndex == vanList.Count)
            {
                currentVanIndex = 0;
            }

            displayVanInfo();
        }

        private async void MainInfo_Load(object sender, EventArgs e)
        {
            //here we want to get a list of vans from firebase, they should have been created at this point assuming the simulation has started
            await InitializeFirebaseConnetion();

            //after firebase function call display data for the first van
            currentVanIndex = 0;
            
            
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void displayVanInfo()
        {
            mutex.WaitOne();
            //System.Diagnostics.Debug.WriteLine("HEY");
            Van van = vanList[currentVanIndex];
            //My idea is that the user will press a button to cycle through the different vans
            //this function will update the MainInfo form to display info on the current van
            richTextBox1.Text = "Van ID: " + van.Vid;
            richTextBox2.Text = "Latitude: " + van.Position.Lat.ToString();
            richTextBox3.Text = "Longitude: " + van.Position.Lng.ToString();
            richTextBox4.Text = "Number of Vials: " + van.Vials;

            mutex.ReleaseMutex();
        }

        private void richTextBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void richTextBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private static async Task InitializeFirebaseConnetion()
        {
            if (vanList.Count == 0)
            {
                mutex.WaitOne();
                System.Diagnostics.Debug.WriteLine("HEREREREREREREREER");
                client = new FirebaseClient("https://proj-109d4-default-rtdb.firebaseio.com/");

                var Vans = await client
                   .Child("Vans")//Prospect list
                   .OnceAsync<Van>();
                foreach (var van in Vans)
                {
                    System.Diagnostics.Debug.WriteLine($"MainInfo: adding van key {van.Key}");
                    vanList.Add(van.Object);
                }
                mutex.ReleaseMutex();

                //this next part will update our vanList eveytime a change is made to Vans in our firebase (I think)
                bool isNew;
                int len = vanList.Count;
                var child = client.Child("Vans");
                var observable = child.AsObservable<Van>();
                var subscriptionFree = observable
                    .Subscribe(van =>
                    {
                        if (van.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
                        {
                            mutex.WaitOne();
                            //System.Diagnostics.Debug.WriteLine($"Updating vanList in MainInfo");

                            isNew = true;
                            for (int i = 0; i < len; i++)
                            {
                                if (van.Key == vanList[i].key)
                                {
                                    //van is already in our list so update it
                                    vanList[i] = van.Object;

                                    isNew = false;
                                    i = len;
                                }
                            }
                            if (isNew == true)
                            {
                                //this van was not found in our list so add it to our list
                                vanList.Add(van.Object);
                            }
                            mutex.ReleaseMutex();
                        }
                    });
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            displayVanInfo();
        }
    }
}
