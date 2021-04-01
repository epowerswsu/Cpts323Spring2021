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
        public string CarPlate;
        public string Vid;
        public Van()
        {
            Position.Lat = 0;
            Position.Lng = 0;
            PositionMarker = new GMarkerGoogle(this.Position, GMarkerGoogleType.orange_dot);
            Vials = 0;
        }
    }
}
