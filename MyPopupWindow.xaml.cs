
using System.Windows;

namespace XliffTranslatorTool
{

    public partial class MyPopupWindow : Window
    {
        public string LangCode
        {
            get
            {
                if (LangCodeTextBox == null) return string.Empty;
                return LangCodeTextBox.Text;
            }
        }
        public MyPopupWindow()
        {
            InitializeComponent();
        }
        private void OnSave(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
