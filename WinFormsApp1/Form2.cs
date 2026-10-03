using System;
using System.Windows.Forms;

namespace WinFormsApp1
{
    public partial class Form2 : Form
    {
        public Form2(string text)
        {
            InitializeComponent();

            label1.Text = ReverseRecursive(text, text.Length - 1);
        }

        private string ReverseRecursive(string text, int index)
        {
            if (index < 0)
                return "";

            return text[index] + ReverseRecursive(text, index - 1);
        }

        private void Form2_Load(object sender, EventArgs e)
        {
        }

    }
}