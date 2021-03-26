using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//may not need all of these GMap.NETs but added them just in case
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using GMap.NET.WindowsForms.ToolTips;


namespace MobileVaccination
{
    public partial class Mainform : Form
    {
        internal readonly GMapOverlay Objects = new GMapOverlay("objects");

        public const int numVans = 6;
        public Van[] vans;

        public Mainform()
        {
            InitializeComponent();
        }

        private void Mainform_Load(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("ENTER MAINFORM_LOAD");
            gMapControl1.MapProvider = GMap.NET.MapProviders.GoogleMapProvider.Instance;
            GMap.NET.GMaps.Instance.Mode = GMap.NET.AccessMode.ServerOnly;
            //gMapControl1.Position = new GMap.NET.PointLatLng(54.6961334816182, 25.2985095977783); // Lithuania, Vilnius
            gMapControl1.Position = new GMap.NET.PointLatLng(46.304302, -119.2752); //Richland
            gMapControl1.Overlays.Add(Objects);
            System.Diagnostics.Debug.WriteLine("EXIT MAINFORM_LOAD");
        }
        
        //function used by button1 in MainForm.designer.cs
        //im thinking we will click this button to start the whole simulation process
        private void startButtonClick(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Clicked Start");
            //create the vans, we need 6 I think?
            vans = new Van[numVans];
            for (int i = 0; i < numVans; i++)
                vans[i] = new Van();

            //this is how you add a marker to the map, you have to add the van's GMapMarker object to a GMapOverlay object,
            vans[0].Position = new GMap.NET.PointLatLng(46.304302, -119.2752);
            vans[0].PositionMarker.Position = vans[0].Position;
            Objects.Markers.Add(vans[0].PositionMarker);

        }

    }
}
