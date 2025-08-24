using DoW_DE_Nod_Manager;
using System;
using System.Windows.Forms;

namespace DoW_DE_Mod_Manager
{
    public partial class StartExpansionForm : Form
    {
        readonly ModManagerForm modManager;

        public StartExpansionForm(ModManagerForm form)
        {
            InitializeComponent();

            modManager = form;
        }

        private void StartOriginalButton_Click(object sender, EventArgs e)
        {
            modManager.StartGameWithOptions("w40k");
            Close();
        }

        private void StartWAButton_Click(object sender, EventArgs e)
        {
            modManager.StartGameWithOptions("WXP");
            Close();
        }

        private void StartDCButton_Click(object sender, EventArgs e)
        {
            modManager.StartGameWithOptions("DXP2");
            modManager.EnableStartExpansionButton();
            Close();
        }

        private void StartExpansionForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            modManager.EnableStartExpansionButton();
        }
    }
}
