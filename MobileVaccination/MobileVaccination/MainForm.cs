using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
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


namespace MobileVaccination
{
    public partial class Mainform : Form
    {
        public string googleMapsApiKey = "AIzaSyDuTNuYTmTllYLf8e71hHdohX-uQJScdJE";
        internal readonly GMapOverlay Objects = new GMapOverlay("objects");
        internal readonly GMapOverlay Routes = new GMapOverlay("routes");
        private const int numVans = 6;
        private Van[] vans;
        private static List<Appointment> appointmentList = new List<Appointment>();

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
            gMapControl1.Overlays.Add(Objects);
            gMapControl1.Overlays.Add(Routes);
            
        }
        
        private async void startButtonClick(object sender, EventArgs e)
        {
            //function used by button1 in MainForm.designer.cs
            //im thinking we will click this button to start the whole simulation process, so its basically treat it like main() 

            System.Diagnostics.Debug.WriteLine("Clicked Start");

            //create the vans, we need 6 I think?
            vans = new Van[numVans];
            for (int i = 0; i < numVans; i++)
            {
                vans[i] = new Van();
                vans[i].Vid = i.ToString();
            }

            //this is how you add a marker to the map, you have to add the van's GMapMarker object to a GMapOverlay object,
            vans[0].Position = new GMap.NET.PointLatLng(46.333050, -119.283240);
            vans[0].PositionMarker.Position = vans[0].Position;
            Objects.Markers.Add(vans[0].PositionMarker);

            //this function uses async, so we have to use await to wait for it because it will do some stuff on different threads
            await InitializeFirebase();

            //test that the appointmentList actually contains appointments
            for(int i = 0; i < appointmentList.Count; i++)
            {
                System.Diagnostics.Debug.WriteLine($"appointment# {i}");
                System.Diagnostics.Debug.WriteLine($"prospect.name: { appointmentList[i].prospect.FirstName}");
                System.Diagnostics.Debug.WriteLine($"destinationName: { appointmentList[i].destination.destinationName}");
                System.Diagnostics.Debug.WriteLine($"destination lat: { appointmentList[i].destination.lat}");
                System.Diagnostics.Debug.WriteLine($"destination long: { appointmentList[i].destination.lon}");
                System.Diagnostics.Debug.WriteLine($"InitialTime: { appointmentList[i].InitialTime}");
                System.Diagnostics.Debug.WriteLine($"active: { appointmentList[i].active}");
                System.Diagnostics.Debug.WriteLine($"acepted: { appointmentList[i].acepted}");
                System.Diagnostics.Debug.WriteLine($"vaccinated: { appointmentList[i].vaccinated}");
            }

            //test AddRoute()
            appointmentList[0].destination.lat = 46.251740;
            appointmentList[0].destination.lon = -119.117540;
            System.Diagnostics.Debug.WriteLine($"lat {appointmentList[0].destination.lat} lon {appointmentList[0].destination.lon}");
            DisplayVanRoute(vans[0], appointmentList[0]);
        }

        private static async Task InitializeFirebase()
        {
            //this will fill the appointmentList with Appointment objects, filled with the appointments from firebase
            //it will also subscribe our company/team to the database

            System.Diagnostics.Debug.WriteLine("ENTER InitializeFirebase");

            //******************** Initialization ***************************//
            var client = new FirebaseClient("https://cpts323battle.firebaseio.com/");
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
                //later we should probably change so it only adds appointments that are not already accepted
                Appointment ap = new Appointment()
                {
                    prospect = appointment.Object.prospect,
                    destination = appointment.Object.destination,
                    pointList = appointment.Object.pointList,
                    InitialTime = appointment.Object.InitialTime,
                    acepted = appointment.Object.acepted,
                    vaccinated = appointment.Object.vaccinated,
                    active = appointment.Object.active,
                };

                appointmentList.Add(ap);
            }

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



        }

        private void DisplayVanRoute(Van van, Appointment appointment)
        {
            PointLatLng startPoint = van.Position;
            PointLatLng endPoint = new PointLatLng(appointment.destination.lat, appointment.destination.lon);

            //var rp = GMapProviders.OpenStreetMap as RoutingProvider;
            var rp = gMapControl1.MapProvider as RoutingProvider;
            
            var route = rp.GetRoute(startPoint, endPoint, false, false, (int)gMapControl1.Zoom);

            if(route != null)
            {
                System.Diagnostics.Debug.WriteLine("adding route");
                // add route
                var r = new GMapRoute(route.Points, route.Name);
                r.IsHitTestVisible = true;
                r.Stroke.Color = Color.Blue;
                Routes.Routes.Add(r);

                // add route start/end marks
                GMapMarker m1 = new GMarkerGoogle(startPoint, GMarkerGoogleType.green_dot);
                m1.ToolTipText = "Van " + van.Vid;
                m1.ToolTipMode = MarkerTooltipMode.Always;

                GMapMarker m2 = new GMarkerGoogle(endPoint, GMarkerGoogleType.red_dot);
                m2.ToolTipText = "Appointment " + appointment.prospect.uid;
                m2.ToolTipMode = MarkerTooltipMode.Always;

                Objects.Markers.Add(m1);
                Objects.Markers.Add(m2);

                gMapControl1.ZoomAndCenterRoute(r);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("route was null");
            }
        }

    }
}
