using System;
using System.Windows.Forms;

namespace WinFormsApp1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            string input = textBox1.Text;

            Form2 form2 = new Form2(input);
            form2.Show();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}