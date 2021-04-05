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

namespace MobileVaccination
{
    public partial class MainInfo : Form
    {
        private static List<Van> vanList = new List<Van>();
        private int currentVanIndex;
        private int numVans;

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
            if(currentVanIndex == numVans)
            {
                currentVanIndex = 0;
            }

            displayVanInfo(vanList[currentVanIndex]);
        }

        private void MainInfo_Load(object sender, EventArgs e)
        {
            
            //here we want to get a list of vans from firebase, they should have been created at this point assuming the simulation has started
            //for now we will just create some vans (replace later with a function call that reads the vans from firebase and fills the list)
            for (int i = 0; i < 6; i++)
            {
                Van v = new Van()
                {
                    Position = new GMap.NET.PointLatLng(46.333050 + i, -119.283240 + i), //add i just so they have different positions for testing purposes
                    Vid = i.ToString(),
                    Vials = i * 2
                };
                vanList.Add(v);
            }


            //after firebase function call display data for the first van
            currentVanIndex = 0;
            numVans = vanList.Count();
            displayVanInfo(vanList[currentVanIndex]);
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void displayVanInfo(Van van)
        {
            //My idea is that the user will press a button to cycle through the different vans
            //this function will update the MainInfo form to display info on the current van
            richTextBox1.Text = "Van ID: " + van.Vid;
            richTextBox2.Text = "Latitude: " + van.Position.Lat.ToString();
            richTextBox3.Text = "Longitude: " + van.Position.Lng.ToString();
            richTextBox4.Text = "Number of Vials: " + van.Vials;

        }

        private void richTextBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void richTextBox2_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
