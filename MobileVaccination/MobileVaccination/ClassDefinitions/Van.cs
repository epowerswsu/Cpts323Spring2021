using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//may not need all of these GMap.NETs but added them just in case
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using GMap.NET.WindowsForms.ToolTips;

namespace MobileVaccination.ClassDefinitions
{
    public class Van
    {
        public PointLatLng Position;
        public GMapMarker PositionMarker;
        public string Make;
        public string Model;
        public int Vials;
        public double TimeSinceRefill;
        public bool HasAppointment;
        public Appointment appointment;
        public string CarPlate;
        public string Vid;
        public System.Drawing.Color routeColor;
        public GMap.NET.MapRoute route;
        public Van()
        {
            Position.Lat = 0;
            Position.Lng = 0;
            PositionMarker = new GMarkerGoogle(Position, GMarkerGoogleType.green_dot);
            PositionMarker.ToolTipText = Vid;
            PositionMarker.ToolTipMode = MarkerTooltipMode.Always;
            Vials = 0;
            TimeSinceRefill = 0.0;
            HasAppointment = false;
            appointment = null;
            //default routeColor (give each van a unique color after creating them)
            routeColor = System.Drawing.Color.Blue;
        }
    }
}
