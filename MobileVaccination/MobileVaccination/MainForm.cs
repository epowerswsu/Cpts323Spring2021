﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using GMap.NET.WindowsForms.ToolTips;
using Firebase.Database;
using System.Net.Http;
using System.Reactive.Linq;
using MobileVaccination.ClassDefinitions;
using Firebase.Database.Query;//for firebase van adding

namespace MobileVaccination
{
    public partial class Mainform : Form
    {
        public string googleMapsApiKey = "AIzaSyDuTNuYTmTllYLf8e71hHdohX-uQJScdJE";
        internal readonly GMapOverlay Objects = new GMapOverlay("objects");
        internal readonly GMapOverlay Routes = new GMapOverlay("routes");
        private const int numVans = 6;
        private static List<Van> vans = new List<Van>();
        private static List<Appointment> appointmentList = new List<Appointment>();
        public static PointLatLng refillLocation = new PointLatLng(46.280682, -119.290833);  //refill location currently unknown, using Kadlec for now
        private static Mutex mutex = new Mutex(); //mutex needed to prevent multiple threads from modifying the appointment list or van list at the same time
        public delegate void DisplayVanRoutes();
        public DisplayVanRoutes displayRoutesDelegate;
        public static FirebaseClient client, client2;
        public int VaccCount = 0;
        public Mainform()
        {
            InitializeComponent();
        }

        private void Mainform_Load(object sender, EventArgs e)
        {
            gMapControl1.MapProvider = GMap.NET.MapProviders.GoogleMapProvider.Instance;
            GMap.NET.MapProviders.GoogleMapProvider.Instance.ApiKey = googleMapsApiKey;
            GMap.NET.GMaps.Instance.Mode = GMap.NET.AccessMode.ServerOnly;
            gMapControl1.Position = new GMap.NET.PointLatLng(46.304302, -119.2752); //starting position for the map, currently set to Richland
            gMapControl1.Overlays.Add(Routes);
            gMapControl1.Overlays.Add(Objects);
            displayRoutesDelegate = new DisplayVanRoutes(DisplayVanRoutesMethod);
        }

        private async void centerScreenButton(object sender, EventArgs e)
        {
            gMapControl1.ZoomAndCenterRoutes("routes");
            
        }

        private async void startButtonClick(object sender, EventArgs e)
        {
            //function used by button1 in MainForm.designer.cs
            //im thinking we will click this button to start the whole simulation process, so its basically treat it like main() 
            System.Diagnostics.Debug.WriteLine("Clicked Start");

            //this function uses async, so we have to use await to wait for it because it will do some stuff on different threads
            await InitializeFirebase();

            //test that the appointmentList actually contains appointments
            for (int i = 0; i < appointmentList.Count; i++)
            {
                System.Diagnostics.Debug.WriteLine($"appointment# {i}");
                System.Diagnostics.Debug.WriteLine($"key: {appointmentList[i].key}");
                System.Diagnostics.Debug.WriteLine($"prospect.name: { appointmentList[i].prospect.FirstName}");
                System.Diagnostics.Debug.WriteLine($"destinationName: { appointmentList[i].destination.destinationName}");
                System.Diagnostics.Debug.WriteLine($"destination lat: { appointmentList[i].destination.lat}");
                System.Diagnostics.Debug.WriteLine($"destination long: { appointmentList[i].destination.lng}");
                System.Diagnostics.Debug.WriteLine($"InitialTime: { appointmentList[i].InitialTime}");
                System.Diagnostics.Debug.WriteLine($"active: { appointmentList[i].active}");
                System.Diagnostics.Debug.WriteLine($"acepted: { appointmentList[i].acepted}");
                System.Diagnostics.Debug.WriteLine($"vaccinated: { appointmentList[i].vaccinated}");
            }

            //test the function that adds route from van to appointment
            //vans[0].Position = new GMap.NET.PointLatLng(46.333050, -119.283240);
            //DisplayVanRoute(vans[0], appointmentList[0]);

            //add a second route just for fun (using our won custom coordinates for testing)
            //appointmentList[1].destination.lat = 46.225650;
            //appointmentList[1].destination.lng = -119.235250;
            //vans[1].Position = new GMap.NET.PointLatLng(46.222900, -119.218510);
            //DisplayVanRoute(vans[1], appointmentList[1]);

            //move a van, updating its postion every few seconds
            //TravelToDestination(vans[0], appointmentList[0]);

            //There are only 2 appointments in his firebase right now so we need to create some of our own to test the startSimulation() more fully
            appointmentList[1].destination.lat = 46.225650;
            appointmentList[1].destination.lng = -119.235250;
            appointmentList[1].acepted = false;
            appointmentList[1].prospect.uid = 1;

            appointmentList.Add(new Appointment()
            {
                acepted = false,
                destination = new Destination() { lat = 46.272040, lng = -119.187530 },
                prospect = new Patient() { uid = 2 }
            });
            appointmentList.Add(new Appointment()
            {
                acepted = false,
                destination = new Destination() { lat = 46.226030, lng = -119.226760 },
                prospect = new Patient() { uid = 3 }
            });
            appointmentList.Add(new Appointment()
            {
                acepted = false,
                destination = new Destination() { lat = 46.257080, lng = -119.306210 },
                prospect = new Patient() { uid = 4 }
            });
            appointmentList.Add(new Appointment()
            {
                acepted = false,
                destination = new Destination() { lat = 46.340720, lng = -119.279650 },
                prospect = new Patient() { uid = 5 }
            });
            appointmentList.Add(new Appointment()
            {
                acepted = false,
                destination = new Destination() { lat = 46.2382670, lng = -119.1040960 },
                prospect = new Patient() { uid = 6 }
            });

            startSimulation();
        }

        private void startSimulation()
        {
            double expireTime = 6.0; //6 hours?
            var startTime = DateTime.UtcNow;
            int simulationTime = 3; //run for this many minutes

            //running on a seperate thread to the UI can continue to run
            Thread t = new Thread(() =>
            {
                System.Diagnostics.Debug.WriteLine("Starting Simulation");

                while (DateTime.UtcNow - startTime < TimeSpan.FromMinutes(simulationTime)) //while the simulation time limit has not been reached
                {
                    //for each van, check for vans that dont currently have appointments, try to assign new appointments to them
                    mutex.WaitOne();
                    for (int i = 0; i < numVans; i++)
                    {
                        if (vans[i].HasAppointment == false)  //dont need to check the vans that are already doing an appointment (this is multithreaded so that would cause problems)
                        {
                            //check if van needs refill
                            if (vans[i].Vials <= 0 || vans[i].TimeSinceRefill >= expireTime)
                            {
                                //if this van is out of vials or they are expired, go back for refill
                                vans[i].HasAppointment = true;

                                //for now just fake it
                                vans[i].Vials = 6;
                                vans[i].TimeSinceRefill = 0.0;

                                vans[i].HasAppointment = false;
                            }

                            //assign appointment to van
                            for (int j = 0; j < appointmentList.Count(); j++)
                            {
                                //at this point we should have an initial list of appointments
                                if (appointmentList[j].acepted == false)
                                {
                                    //if not already acepted, set it to acepted = true in his firebase, and active = "something cause its a string for some reason"
                                    //...
                                    appointmentList[j].acepted = true;
                                    appointmentList[j].active = "true";
                                    //to do: write changed appointment back to his firebase
                                    vans[i].appointment = appointmentList[j];
                                    vans[i].HasAppointment = true;
                                    System.Diagnostics.Debug.WriteLine($"Van with vid: {vans[i].Vid} took appointment with uid: {vans[i].appointment.prospect.uid}");
                                    break;
                                }
                            }

                            //if van was able to get an appointment then do that appointment
                            if (vans[i].HasAppointment == true)
                            {
                                //if a van gets to this point then it was able to get an appointment
                                //fill that appointment
                                TravelToDestination(vans[i], vans[i].appointment); //van will run on own thread from this function and this loop will continue
                            }
                            else
                            {
                                //the van was not able to get an appointment from our current list
                                //this means our list was out of availible appointments, so read new appointments from his firebase

                            }
                        }
                    }
                    UpdateVansInFirebase(); //update the vans in our firebase
                    mutex.ReleaseMutex();
                    Thread.Sleep(20000); //wait for a few seconds before checking the vans again, so this doesnt hog the mutex
                }
                System.Diagnostics.Debug.WriteLine("Finished Simulation");
            });
            t.Start();
        }

        private static async Task InitializeFirebase()
        {
            //this will fill the appointmentList with Appointment objects, filled with the appointments from firebase
            //it will also subscribe our company/team to the database

            System.Diagnostics.Debug.WriteLine("ENTER InitializeFirebase");

            //******************** Initialization ***************************//
            client = new FirebaseClient("https://cpts323battle.firebaseio.com/");
            HttpClient httpclient = new HttpClient();
            string selectedkey = "", responseString, companyId;
            FormUrlEncodedContent content;
            HttpResponseMessage response;

            //******************** Get initial list of Prospect ***********************//
            var Appointments = await client
               .Child("appointments")//Prospect list
               .OnceAsync<Appointment>();
            foreach (var appointment in Appointments)
            {
                System.Diagnostics.Debug.WriteLine($"OA1:{appointment.Key}:->{appointment.Object.acepted}");
                //create a list of appointments
                //smart selection to improve your profit
                selectedkey = appointment.Key;

                //create Appointment object, set all of its memebers, and add it to the appointmentList
                Appointment ap = new Appointment()
                {
                    prospect = appointment.Object.prospect,
                    destination = appointment.Object.destination,
                    pointList = appointment.Object.pointList,
                    InitialTime = appointment.Object.InitialTime,
                    acepted = appointment.Object.acepted,
                    vaccinated = appointment.Object.vaccinated,
                    active = appointment.Object.active,
                    key = appointment.Key,
                };

                appointmentList.Add(ap);
            }

            //make an observer for the appointments in his firebase, when they are changed this should automatically detect it
            SubscribeToAppointmentListChanges();

            //next step is to subscribe our company/team name to his firebase
            //this will add our company to the database everytime it runs, so keep it commented out so you dont spam his firebase
            var company = new Company
            {
                companyName = "Blue Team",
                status = "active"
            };
            var dictionary = new Dictionary<string, string>
            {
                { "companyName",company.companyName  },
                { "status",company.status}
            };
            //content = new FormUrlEncodedContent(dictionary);
            //response = await httpclient.PostAsync("https://us-central1-cpts323battle.cloudfunctions.net/reportCompany", content);
            //responseString = await response.Content.ReadAsStringAsync();
            //Response data = Newtonsoft.Json.JsonConvert.DeserializeObject<Response>(responseString);

            //System.Diagnostics.Debug.WriteLine(data.message);
            //System.Diagnostics.Debug.WriteLine(data.companyId);
            //companyId = data.companyId;

            //next step is to create our vans and add them to our firebase
            //first create the local Van objects and add them to the "vans" list
            Random rand = new Random();
            for (int i = 0; i < numVans; i++)
            {
                Van van = new Van
                {
                    Position = refillLocation,
                    Vid = "Van " + i,
                    CarPlate = rand.Next(100000, 999999).ToString(), //use a random number for the plate
                    Vials = 6  //whats the max vials a van can hold at a time?
                };
                vans.Add(van);
            }
            vans[0].routeColor = Color.Blue;
            vans[1].routeColor = Color.ForestGreen;
            vans[2].routeColor = Color.Pink;
            vans[3].routeColor = Color.Black;
            vans[4].routeColor = Color.Yellow;
            vans[5].routeColor = Color.Red;

            //this part adds the newly created vans to our firebase
            client2 = new FirebaseClient("https://proj-109d4-default-rtdb.firebaseio.com/");
            HttpClient httpclient2 = new HttpClient();
            for (int i = 0; i < numVans; i++)
            {
                var child2 = client2.Child("Vans");
                var fbVan = await child2.PostAsync(vans[i]);
                vans[i].key = fbVan.Key;
            }
        }

        public void DisplayVanRoutesMethod()
        {
            //use the mutex when changing this stuff because the startSimulation() may also be accessing the appointment or van
            mutex.WaitOne();

            for (int i = 0; i < numVans; i++)
            {
                var van = vans[i];
                if (van.HasAppointment == true && van.HasRoute == false)
                {
                    var appointment = van.appointment;
                    van.HasRoute = true;

                    PointLatLng startPoint = van.Position;
                    PointLatLng endPoint = new PointLatLng(appointment.destination.lat, appointment.destination.lng);

                    var rp = gMapControl1.MapProvider as RoutingProvider;

                    var route = rp.GetRoute(startPoint, endPoint, false, false, (int)gMapControl1.Zoom);

                    if (route != null)
                    {
                        System.Diagnostics.Debug.WriteLine("adding route");
                        // add route
                        var r = new GMapRoute(route.Points, route.Name);
                        r.IsHitTestVisible = true;
                        r.Stroke.Color = van.routeColor;
                        r.Stroke = (Pen)r.Stroke.Clone();
                        Routes.Routes.Add(r);
                        //set the van's route to be this route, so we can move the van from point to point later
                        van.route = route;

                        // add route start/end marks
                        //use the van's position marker
                        Objects.Markers.Remove(van.PositionMarker);     //remove the old marker
                        van.PositionMarker = new GMarkerGoogle(startPoint, GMarkerGoogleType.green_dot);
                        van.PositionMarker.ToolTipText = van.Vid;
                        van.PositionMarker.ToolTipMode = MarkerTooltipMode.Always;

                        GMapMarker m2 = new GMarkerGoogle(endPoint, GMarkerGoogleType.red_dot);
                        m2.ToolTipText = "Appointment " + appointment.prospect.uid;
                        m2.ToolTipMode = MarkerTooltipMode.Always;

                        Objects.Markers.Add(van.PositionMarker);
                        Objects.Markers.Add(m2);

                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("route was null");
                    }
                }
            }

            mutex.ReleaseMutex();
        }

        private void TravelToDestination(Van van, Appointment appointment)
        {
            //every thing in here runs in its own thread so it doesn't freeze up the Mainform's thread
            //use the mutex when changing this stuff because the startSimulation() may also be accessing the appointment or van
            Thread t = new Thread(() =>
            {
                Invoke(displayRoutesDelegate); //call the route adding function using the ui thread

                for (int i = 0; i < van.route.Points.Count; i++)
                {
                    Thread.Sleep(1000);    //after a few seconds the postion is updated
                    
                    
                    //makes sense to do calcs here
                    double dist = DistanceTo(van.route.Points[0].Lat, van.route.Points[0].Lng, van.route.Points[i].Lat, van.route.Points[i].Lng);
                    //just for proof of concept, printing to Tbox
                    textBox3.Invoke(new Action(() => textBox3.Text = dist.ToString()));


                    mutex.WaitOne();
                    van.Position = van.route.Points[i]; //move to next point in point list
                    van.PositionMarker.Position = van.Position;  //move the marker's position also
                    UpdateVan(van); //could make this await, but small delay is not important

                    mutex.ReleaseMutex();
                }

                //finished the appointment
                mutex.WaitOne();
                appointment.vaccinated = "true";
                appointment.active = "false";
                van.HasAppointment = false;
                van.HasRoute = false;
                van.Vials--;
                System.Diagnostics.Debug.WriteLine($"Van {van.Vid} finished appointment {appointment.prospect.uid}");
                VaccCount++;

                //update main screen
                _ = textBox1.Invoke(new Action(() => textBox1.Text = VaccCount.ToString()));
                mutex.ReleaseMutex();
            });
            t.Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Hide();
            MainInfo maininfo = new MainInfo();
            maininfo.ShowDialog();
            this.Show();
        }

        private static async Task UpdateVansInFirebase()
        {
            //update the vans in our firebase to match the actual vans in our simulation
            foreach (Van van in vans) //foreach creates and uses a copy of the vans list, doesnt use the real list
            {
                //System.Diagnostics.Debug.WriteLine($"updating key: {van.key}");
                var child = client2.Child("/Vans/" + van.key + "/");
                await child.PutAsync(van);
            }

        }

        //updates individual vans to FB. could eventually report to Torre's FB too
        private static async Task UpdateVan(Van van)
        {
            //uncomment following line to see if successful
            //System.Diagnostics.Debug.WriteLine($"updating key SOLO: {van.key}");
            var child = client2.Child("/Vans/" + van.key + "/");
            await child.PutAsync(van);

        }

        public static double DistanceTo(double lat1, double lon1, double lat2, double lon2)
        {
            double rlat1 = Math.PI * lat1 / 180;
            double rlat2 = Math.PI * lat2 / 180;
            double theta = lon1 - lon2;
            double rtheta = Math.PI * theta / 180;
            double dist =
                Math.Sin(rlat1) * Math.Sin(rlat2) + Math.Cos(rlat1) *
                Math.Cos(rlat2) * Math.Cos(rtheta);
            dist = Math.Acos(dist);
            dist = dist * 180 / Math.PI;
            dist = dist * 60 * 1.1515;

            return dist;
        }



        private static void SubscribeToAppointmentListChanges()
        {
            //constantly checks for appointment changes in his firebase, function only needs to be called once i think
            bool isNew;
            int len = appointmentList.Count;
            var child = client.Child("appointments");
            var observable = child.AsObservable<Appointment>();
            var subscriptionFree = observable
                .Where(f => !string.IsNullOrEmpty(f.Key)) // you get empty Key when there are no data on the server for specified node
                .Where(f => f.Object?.acepted == false)
                .Subscribe(appointment =>
                {
                    if (appointment.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
                    {
                        Console.WriteLine($"Updating appointmentList:{appointment.Key}:->{appointment.Object.destination.destinationName}");

                        isNew = true;
                        for (int i = 0; i < len; i++)
                        {
                            if (appointment.Key == appointmentList[i].key)
                            {
                                //for appointments that are already in our list, update them by reading from the firebase
                                System.Diagnostics.Debug.WriteLine($"updating appointment {appointment.Key} in our list");
                                appointmentList[i].prospect = appointment.Object.prospect;
                                appointmentList[i].destination = appointment.Object.destination;
                                appointmentList[i].pointList = appointment.Object.pointList;
                                appointmentList[i].InitialTime = appointment.Object.InitialTime;
                                appointmentList[i].acepted = appointment.Object.acepted;
                                appointmentList[i].vaccinated = appointment.Object.vaccinated;
                                appointmentList[i].active = appointment.Object.active;
                                appointmentList[i].key = appointment.Key;

                                isNew = false;
                                i = len;
                            }
                        }
                        if (isNew == true)
                        {
                            System.Diagnostics.Debug.WriteLine($"adding new appointment {appointment.Key} to our list");
                            //this appointment was not found in our list so add it to our list
                            Appointment ap = new Appointment()
                            {
                                prospect = appointment.Object.prospect,
                                destination = appointment.Object.destination,
                                pointList = appointment.Object.pointList,
                                InitialTime = appointment.Object.InitialTime,
                                acepted = appointment.Object.acepted,
                                vaccinated = appointment.Object.vaccinated,
                                active = appointment.Object.active,
                                key = appointment.Key,
                            };
                            appointmentList.Add(ap);
                        }
                    }
                });
        }
    }
}
