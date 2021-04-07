using Firebase.Database;

namespace MobileVaccination.ClassDefinitions
{
    using Newtonsoft.Json;
    public class Appointment
    {


        public Patient prospect { get; set; }
        public Destination destination { get; set; }
        public Point[] pointList { get; set; }
        public string InitialTime { get; set; }
        public bool acepted { get; set; }
        public string vaccinated { get; set; }
        public string active { get; set; }

        public int key { get; set; }

    }

    public class Patient
    {
        public string userName;
        public string FirstName;
        public string LastName;
        public string SS;
        public string Age;
        public int uid;
        public string image;
        public string userCellphone;
    }
    public class Driver
    {
        public int companyColor;

    }

    public class Point
    {
        public double lat;
        public double lng;
        public double time;
    }
    public class Destination
    {
        public double lat;
        public double lng;
        public string destinationName;
    }
    public class Company
    {
        public string companyName { get; set; }
        public string status { get; set; }
    }
    public class Status
    {
        public int code { get; set; }
        public ServerTimeStamp time { get; } = new ServerTimeStamp();
    }

    public class VanInfo//prototype to add van info
    {
        public int plate { get; set; }
        public string id { get; set; }
        public ServerTimeStamp time { get; } = new ServerTimeStamp();
    }
    public class Response
    {
        public bool success;
        public int index;
        public string message;
        public string companyId;
        public string color;
    }

}

