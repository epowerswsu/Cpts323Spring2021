using System;
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
        public static string companyId;
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
            //appointmentList[1].destination.lat = 46.225650;
            //appointmentList[1].destination.lng = -119.235250;
            //appointmentList[1].acepted = false;
            //appointmentList[1].prospect.uid = 1;

            //appointmentList.Add(new Appointment()
            //{
            //    acepted = false,
            //    destination = new Destination() { lat = 46.272040, lng = -119.187530 },
            //    prospect = new Patient() { uid = 2 }
            //});
            //appointmentList.Add(new Appointment()
            //{
            //    acepted = false,
            //    destination = new Destination() { lat = 46.226030, lng = -119.226760 },
            //    prospect = new Patient() { uid = 3 }
            //});
            //appointmentList.Add(new Appointment()
            //{
            //    acepted = false,
            //    destination = new Destination() { lat = 46.257080, lng = -119.306210 },
            //    prospect = new Patient() { uid = 4 }
            //});
            //appointmentList.Add(new Appointment()
            //{
            //    acepted = false,
            //    destination = new Destination() { lat = 46.340720, lng = -119.279650 },
            //    prospect = new Patient() { uid = 5 }
            //});
            //appointmentList.Add(new Appointment()
            //{
            //    acepted = false,
            //    destination = new Destination() { lat = 46.2382670, lng = -119.1040960 },
            //    prospect = new Patient() { uid = 6 }
            //});

            startSimulation();
        }

        private void startSimulation()
        {
            double expireTime = 1350; //6 hours is 1350 seconds in sim time (6*60*60) / 16 
            //double expireTime = 60;
            var startTime = DateTime.UtcNow;
            int simulationTime = 30; //run for this many minutes

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
                        //update each vans time since refill

                        if (vans[i].HasAppointment == false)  //dont need to check the vans that are already doing an appointment (this is multithreaded so that would cause problems)
                        {
                            //check if van needs refill
                            if (vans[i].Vials <= 0 || (DateTime.UtcNow - vans[i].TimeOfLastRefill) >= TimeSpan.FromSeconds(expireTime))
                            {
                                //if this van is out of vials or they are expired, go back for refill
                                vans[i].HasAppointment = true;
                                TravelToRefill(vans[i]);
                            }
                            else
                            {
                                //assign appointment to van if it doesnt need refill
                                for (int j = 0; j < appointmentList.Count(); j++)
                                {
                                    //at this point we should have an initial list of appointments
                                    if (appointmentList[j].acepted == false)
                                    {
                                        //if not already acepted, set it to acepted = true in his firebase
                                        SelectAppointment(appointmentList[j], vans[i]);
                                        //...
                                        appointmentList[j].acepted = true;
                                        appointmentList[j].active = "true";
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
                    }
                    UpdateVansInFirebase(); //update the vans in our firebase
                    UpdatePositionFirebase(); //update position of our vans in his firebase

                    mutex.ReleaseMutex();
                    Thread.Sleep(5000); //wait for 5 seconds before checking the vans again, so this doesnt hog the mutex, also he wants the van position updated in his firebase every 5 seconds
                }
                System.Diagnostics.Debug.WriteLine("Finished Simulation");
            });
            t.Start();
        }

        //private static bool ChooseAppoinment(int totalSimTime, Appointment appointment)
        //{

        //    return false;
        //}

        private static async Task InitializeFirebase()
        {
            //this will fill the appointmentList with Appointment objects, filled with the appointments from firebase
            //it will also subscribe our company/team to the database

            System.Diagnostics.Debug.WriteLine("ENTER InitializeFirebase");

            //******************** Initialization ***************************//
            client = new FirebaseClient("https://cpts323battle.firebaseio.com/");
            HttpClient httpclient = new HttpClient();
            string selectedkey = "", responseString;
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
            content = new FormUrlEncodedContent(dictionary);
            response = await httpclient.PostAsync("https://us-central1-cpts323battle.cloudfunctions.net/reportCompany", content);
            responseString = await response.Content.ReadAsStringAsync();
            Response data = Newtonsoft.Json.JsonConvert.DeserializeObject<Response>(responseString);

            System.Diagnostics.Debug.WriteLine(data.message);
            System.Diagnostics.Debug.WriteLine(data.companyId);
            companyId = data.companyId;

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
            Thread t = new Thread(async () =>
            {
                double sleepTime;

                Invoke(displayRoutesDelegate); //call the route adding function using the ui thread

                for (int i = 0; i < van.route.Points.Count; i++)
                {
                                      
                    //makes sense to do calcs here
                    double dist = DistanceTo(van.route.Points[0].Lat, van.route.Points[0].Lng, van.route.Points[i].Lat, van.route.Points[i].Lng); //get distance in miles
                    //just for proof of concept, printing to Tbox
                    //textBox3.Invoke(new Action(() => textBox3.Text = dist.ToString()));
                    //35mph = 0.00972222 miles per second, t = d/v,
                    sleepTime = dist / 0.00972222; //time in seconds = miles / velocity(in miles per second)
                    sleepTime = sleepTime * 1000; //convert seconds to milliseconds
                    sleepTime = sleepTime / 16; //divide by 16 to translate it to simulation time
                    //System.Diagnostics.Debug.WriteLine($"{van.Vid} sleeping for {sleepTime}");
                    Thread.Sleep(Convert.ToInt32(sleepTime));    //after a few seconds the postion is updated, the time used here should depend on the distance between the two points


                    mutex.WaitOne();
                    van.Position = van.route.Points[i]; //move to next point in point list
                    van.PositionMarker.Position = van.Position;  //move the marker's position also
                    UpdateVan(van); //could make this await, but small delay is not important

                    mutex.ReleaseMutex();
                }

                //arrive at patient's house, code 100
                System.Diagnostics.Debug.WriteLine($"Van {van.Vid} arrived at appointment {appointment.prospect.uid}");
                var child = client.Child("/appointmentsStatus/" + appointment.key + "/status/0");
                var status = new Status
                {
                    code = 100
                };
                await child.PutAsync(status);
                Thread.Sleep(18750); //take 5 mins to vaccinate, 5 sim minutes => (5*60)/16 = 18.75 real seconds

                //patient vaccinated, code 110
                System.Diagnostics.Debug.WriteLine($"Van {van.Vid} waiting at appointment {appointment.prospect.uid}");
                var child2 = client.Child("/appointmentsStatus/" + appointment.key + "/status/0");
                var status2 = new Status
                {
                    code = 110
                };
                await child2.PutAsync(status2);
                Thread.Sleep(56250); //take 15 mins of supervision, 15 sim minutes => (15*60)/16 = 56.25 real seconds

                //finished the appointment and patient supervision, code 120
                var child3 = client.Child("/appointmentsStatus/" + appointment.key + "/status/0");
                var status3 = new Status
                {
                    code = 120
                };
                await child3.PutAsync(status3);

                mutex.WaitOne();
                appointment.vaccinated = "true";
                appointment.active = "false";
                van.HasAppointment = false;
                van.HasRoute = false;
                van.Vials--;
                System.Diagnostics.Debug.WriteLine($"Van {van.Vid} finished appointment {appointment.prospect.uid}");
                VaccCount++;
                FinishAppointment(appointment, van);
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
        private static async Task Delete()

        {
            var child = client2.Child("/Vans/");
            await child.DeleteAsync();
            System.Environment.Exit(0);
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

                        mutex.WaitOne();

                        isNew = true;
                        len = appointmentList.Count;
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

                        mutex.ReleaseMutex();
                    }
                });
        }

        private async Task SelectAppointment(Appointment appointment, Van van)
        {
            //use this firebase function to accept an appointment
            HttpClient httpclient = new HttpClient();

            //******************* call cloud function select a appointment by id ****************/
            var dictionary = new Dictionary<string, string>
                     {
                         { "key",appointment.key  },
                         { "carPlate",van.CarPlate  },
                         { "vid",van.Vid },
                         { "company","Blue Team"},
                         { "companyId",companyId},
                         { "image","http:.."}
                     };
            //System.Diagnostics.Debug.WriteLine($"key: {appointment.key} comapnyId: {companyId}");
            var content = new FormUrlEncodedContent(dictionary);
            var response = await httpclient.PostAsync("https://us-central1-cpts323battle.cloudfunctions.net/selectAppointmentById", content); //this will set the selected appointment's acepted = true
            var responseString = await response.Content.ReadAsStringAsync();
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Response>(responseString);
            //System.Diagnostics.Debug.WriteLine(data.message);
        }

        //he wants us to update the position every 5 seconds
        private async Task UpdatePositionFirebase()
        {
            HttpClient httpclient = new HttpClient();

            foreach (Van van in vans)
            {
                //System.Diagnostics.Debug.WriteLine($"updating van {van.Vid} in his firebase");
                //******************* Call Cloud Function updatePosition ****************/

                var dictionary = new Dictionary<string, string>
                {
                 { "key",van.appointment.key },
                 { "vid",van.Vid},
                 { "companyId",companyId},
                 { "lat",van.Position.Lat.ToString()},
                 { "lng",van.Position.Lng.ToString()}
                };

                var content = new FormUrlEncodedContent(dictionary);
                var response = await httpclient.PostAsync("https://us-central1-cpts323battle.cloudfunctions.net/updatePosition", content);
                var responseString = await response.Content.ReadAsStringAsync();
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Response>(responseString);
                //System.Diagnostics.Debug.WriteLine(data.message);
                //System.Diagnostics.Debug.WriteLine("Current index for the point " + data.index);
            }
        }

        private void button4_Click_1(object sender, EventArgs e)
        {
            Delete();
        }

        private async Task FinishAppointment(Appointment appointment, Van van)
        {
            //write to his firebase to finish an appointment
            HttpClient httpclient = new HttpClient();
            //******************* Call Cloud Function for finishAppointment ****************/


            var dictionary = new Dictionary<string, string>
            {
                { "key",appointment.key  },
                { "vid",van.Vid},
                { "companyId",companyId},
                { "lat",van.Position.Lat.ToString()},
                { "lng",van.Position.Lng.ToString()}
            };

            var content = new FormUrlEncodedContent(dictionary);
            var response = await httpclient.PostAsync("https://us-central1-cpts323battle.cloudfunctions.net/finishAppointment", content);
            var responseString = await response.Content.ReadAsStringAsync();
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Response>(responseString);
            //System.Diagnostics.Debug.WriteLine(data.message);
        }

        private void TravelToRefill(Van van)
        {
            //every thing in here runs in its own thread so it doesn't freeze up the Mainform's thread
            //this function is like the TravelToDestination except it travels to the refill location and doesnt have to connect to firebase
            Thread t = new Thread(async () =>
            {
                System.Diagnostics.Debug.WriteLine($"{van.Vid} is refilling");
                double sleepTime;
                //create an appointment object for the refill
                Appointment refill = new Appointment()
                {
                    prospect = new Patient()
                    {
                        uid = 000,
                    },
                    destination = new Destination(){lat = refillLocation.Lat, lng = refillLocation.Lng, destinationName = "Vaccine Refill" },
                    //pointList = appointment.Object.pointList,
                    //InitialTime = appointment.Object.InitialTime,
                    //acepted = appointment.Object.acepted,
                    //vaccinated = appointment.Object.vaccinated,
                    //active = appointment.Object.active,
                    //key = appointment.Key,
                };
                van.appointment = refill;

                Invoke(displayRoutesDelegate); //call the route adding function using the ui thread

                for (int i = 0; i < van.route.Points.Count; i++)
                {

                    //makes sense to do calcs here
                    double dist = DistanceTo(van.route.Points[0].Lat, van.route.Points[0].Lng, van.route.Points[i].Lat, van.route.Points[i].Lng); //get distance in miles
                                                                                                                                                  //just for proof of concept, printing to Tbox
                    //textBox3.Invoke(new Action(() => textBox3.Text = dist.ToString()));
                    //35mph = 0.00972222 miles per second, t = d/v,
                    sleepTime = dist / 0.00972222; //time in seconds = miles / velocity(in miles per second)
                    sleepTime = sleepTime * 1000; //convert seconds to milliseconds
                    sleepTime = sleepTime / 16;
                    //System.Diagnostics.Debug.WriteLine($"{van.Vid} sleeping for {sleepTime}");
                    Thread.Sleep(Convert.ToInt32(sleepTime));    //after a few seconds the postion is updated, the time used here should depend on the distance between the two points


                    mutex.WaitOne();
                    van.Position = van.route.Points[i]; //move to next point in point list
                    van.PositionMarker.Position = van.Position;  //move the marker's position also
                    UpdateVan(van); //could make this await, but small delay is not important

                    mutex.ReleaseMutex();
                }

                //finished refill
                mutex.WaitOne();
                System.Diagnostics.Debug.WriteLine($"{van.Vid} finished refill");
                van.HasAppointment = false;
                van.HasRoute = false;
                van.Vials = 6;
                van.TimeOfLastRefill = DateTime.UtcNow;
                mutex.ReleaseMutex();

            });
            t.Start();
        }
    }

}
