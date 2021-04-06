using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MobileVaccination
{
    public partial class login : Form
    {
        public login()
        {
            InitializeComponent();
        }

        private void login_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            string PW;
            PW = textBox4.Text;
            string UN;
            UN = textBox3.Text;

            if (PW != "" && UN != "")
            {
                this.Hide();
                Mainform mainform = new Mainform();
                mainform.ShowDialog();
                this.Close();
            }
            else
            {
                string WARN;
                WARN = "Must enter all fields";
                textBox5.Text = WARN;
            }
            
        }

        
    }
}
