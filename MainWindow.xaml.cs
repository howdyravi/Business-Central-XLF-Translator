using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Xml;
using XliffTranslatorTool.Parser;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Controls;
using System.Text;

namespace XliffTranslatorTool
{
    public class MyEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
    }
    public partial class MainWindow : Window
    {
        private enum State
        {
            Loaded,
            FileOpened
        }

        private string OpenedFileName { get; set; }
        private XliffParser XliffParser { get; set; } = new XliffParser();
        private State _currentState;
        private State CurrentState
        {
            get => _currentState;
            set
            {
                _currentState = value;
                OnStateChanged();
            }
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SetState(State.Loaded);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (MainDataGrid.HasItems)
            {
                MessageBoxResult messageBoxResult = MessageBox.Show("Save as new file ?", "Question", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                switch (messageBoxResult)
                {
                    case MessageBoxResult.None:
                    case MessageBoxResult.Cancel:
                        {
                            e.Cancel = true;
                            break;
                        }
                    case MessageBoxResult.Yes:
                        {
                           
                            SaveAs("", GetLanguageCode());
                            break;
                        }
                    case MessageBoxResult.No:
                        {
                            e.Cancel = false;
                            break;
                        }
                }
            }
        }

        private void OnStateChanged()
        {
            switch (CurrentState)
            {
                case State.Loaded:
                    {
                        ImportFileMenuOption.IsEnabled = false;
                        SaveAsMenuOption.IsEnabled = false;
                        SaveMenuOption.IsEnabled = false;
                        TranslateMenuOption.IsEnabled = false;
                        TranslateEnglishOption.IsEnabled = false;
                        ExportUntranslated.IsEnabled = false;
                        MainDataGrid.Visibility = Visibility.Hidden;
                        break;
                    }
                case State.FileOpened:
                    {
                        ImportFileMenuOption.IsEnabled = true;
                        SaveAsMenuOption.IsEnabled = true;
                        SaveMenuOption.IsEnabled = true;
                        TranslateMenuOption.IsEnabled = true;
                        TranslateEnglishOption.IsEnabled = true;
                        ExportUntranslated.IsEnabled = true;
                        MainDataGrid.Visibility = Visibility.Visible;
                        break;
                    }
                default:
                    throw new NotImplementedException($"WindowState '{CurrentState.ToString()}' not implemented");
            }
        }

        private void SetState(State state)
        {
            CurrentState = state;
        }

        private void OpenFileMenuOption_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentState == State.FileOpened)
            {
                MessageBoxResult messageBoxResult = MessageBox.Show("Opening a new file will overwrite your current data and your changes will be lost.\nContinue ?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (messageBoxResult == MessageBoxResult.No)
                {
                    return;
                }
            }

            OpenFile();
        }

        private void OpenFile()
        {
            OpenFileDialog openFileDialog = CreateOpenFileDialog();
            bool? result = openFileDialog.ShowDialog();

            string filePath = openFileDialog.FileName;
            OpenedFileName = openFileDialog.SafeFileName;

            if (result == true)
            {
                ObservableCollection<TranslationUnit> translationUnits = XliffParser.GetTranslationUnitsFromFile(filePath);
                if (AreTranslationUnitsValid(translationUnits))
                {
                    MainDataGrid.ItemsSource = translationUnits;
                    SetState(State.FileOpened);
                }
            }
        }

        private async void ImportFileMenuOption_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult messageBoxResult = MessageBox.Show("This will Update existing records if ID of translation unit matches !\n Click YES if you agree.", "Import mode", MessageBoxButton.YesNo, MessageBoxImage.Question);
            MenuItem mi = e.Source as MenuItem;
            String HelperFileOwner = mi.Name;
            switch (messageBoxResult)
            {
                case MessageBoxResult.Yes:
                    {
                        var progress = new Progress<int>(value =>
                        {
                            _progressBar.Value = value;
                            _progressPercentage.Text = value.ToString() + "%";
                        });
                        await Task.Run(() => ImportFile(progress, true, HelperFileOwner));

                        break;
                    }
                case MessageBoxResult.No:
                    {
                        break;
                    }
                case MessageBoxResult.None:
                    return;
                default:
                    throw new NotImplementedException($"Not implemented MessageBoxResult '{messageBoxResult.ToString()}'");
            }
        }

        private void ImportFile(IProgress<int> progress, bool updateExisting, String FileOwner)
        {
            OpenFileDialog openFileDialog = CreateOpenFileDialog();
            openFileDialog.Title = "Import";
            bool? result = openFileDialog.ShowDialog();

            string filePath = openFileDialog.FileName;

            if (result == true)
            {
                this.Dispatcher.Invoke((Action)(() =>
                {
                    _progressPanel.Visibility = Visibility.Visible;
                    _progressBar.Value = 0;
                    _progressPercentage.Text = "0%";
                }));

                ObservableCollection<TranslationUnit> newTranslationUnits = XliffParser.GetTranslationUnitsFromFile(filePath);
                if (AreTranslationUnitsValid(newTranslationUnits))
                {
                    ObservableCollection<TranslationUnit> list = (ObservableCollection<TranslationUnit>)MainDataGrid.ItemsSource;

                    int count = newTranslationUnits.Count;
                    int current = 0;

                    foreach (TranslationUnit translationUnit in newTranslationUnits)
                    {
                        current++;

                        if (list.Contains(translationUnit) && updateExisting)
                        {
                            TranslationUnit originalTranslationUnit = list.Where(otu => otu.Identifier == translationUnit.Identifier).FirstOrDefault();

                            if (originalTranslationUnit != null && translationUnit.Target == "" && FileOwner == "Microsoft")
                            {
                                originalTranslationUnit.Description = "Locked";
                                originalTranslationUnit.Target = "";
                            }
                            else if (originalTranslationUnit != null && originalTranslationUnit.Source == translationUnit.Source)
                            {
                                originalTranslationUnit.Description = translationUnit.Description;
                                originalTranslationUnit.Meaning = translationUnit.Meaning;
                                originalTranslationUnit.Target = translationUnit.Target;
                            }
                        }
                        var percentageComplete = (current * 100) / count;
                        progress.Report(percentageComplete);
                    }
                    MessageBox.Show("Translations has been successfully applied", "", MessageBoxButton.OK, MessageBoxImage.Information);

                    this.Dispatcher.Invoke((Action)(() =>
                    {
                        _progressPanel.Visibility = Visibility.Hidden;
                        MainDataGrid.Items.Refresh();
                    }));
                }
            }
        }

        private bool AreTranslationUnitsValid(IList<TranslationUnit> translationUnits)
        {
            if (translationUnits == null)
            {
                MessageBox.Show($"XLIFF version was not recognized. Supported versions are: {String.Join(", ", Constants.XLIFF_VERSION_V12, Constants.XLIFF_VERSION_V20)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (translationUnits.Count == 0)
            {
                MessageBox.Show("0 translations found", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                return true;
            }

            return false;
        }

        private void SaveAsMenuOption_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = e.Source as MenuItem;
            SaveAs(mi.Name, GetLanguageCode());
        }

        private void SaveAs(String option, String LangCode)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                FileName = OpenedFileName,
                DefaultExt = Constants.FILE_DIALOG_DEFAULT_EXT,
                Filter = Constants.FILE_DIALOG_FILTER,
                CheckPathExists = true,
                OverwritePrompt = true,
                AddExtension = true
            };

            bool? result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                XmlDocument xmlDocument = XliffParser.CreateXliffDocument(XliffParser.XliffVersion.V12, MainDataGrid.ItemsSource, option, LangCode);

                Save(xmlDocument, saveFileDialog.FileName);
            }
        }

        private void SaveMenuOption_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = e.Source as MenuItem;
            Save(XliffParser.CreateXliffDocument(XliffParser.GetLastFileXliffVersion(), MainDataGrid.ItemsSource, mi.Name, GetLanguageCode()), XliffParser.GetLastFilePath());
        }

        private void Save(XmlDocument xmlDocument, string filePath)
        {
            WriteToFile(xmlDocument, filePath);
            MessageBox.Show("Saved", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void WriteToFile(XmlDocument xmlDocument, string filePath)
        {
            StringBuilder StringBuilder = new StringBuilder();
            ExtentedStringWriter ExtentedStringWriter = new ExtentedStringWriter(StringBuilder, Encoding.UTF8);
            xmlDocument.Save(ExtentedStringWriter);
            string indented = ExtentedStringWriter.ToString().Replace("_AMP;_", "&").Replace("_LT;_", "<").Replace("_GT;_", ">");
            using (StreamWriter streamWriter = new StreamWriter(filePath, false))
            {
                streamWriter.Write(indented);
            }
        }

        private static OpenFileDialog CreateOpenFileDialog()
        {
            return new OpenFileDialog
            {
                DefaultExt = Constants.FILE_DIALOG_DEFAULT_EXT,
                Filter = Constants.FILE_DIALOG_FILTER,
                Multiselect = false
            };
        }

        private async void TranslateMenuOption_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult messageBoxResult = MessageBox.Show("This will overwrite the conflicting translation.\nChoose 'Yes' if you agree.", "Translate Mode", MessageBoxButton.YesNo, MessageBoxImage.Question);

            switch (messageBoxResult)
            {
                case MessageBoxResult.Yes:
                    {
                        var progress = new Progress<int>(value =>
                        {
                            _progressBar.Value = value;
                            _progressPercentage.Text = value.ToString() + "%";
                        });
                        await Task.Run(() => TranslateFile(progress));

                        break;
                    }
                case MessageBoxResult.No:
                    {
                        break;
                    }
                case MessageBoxResult.None:
                    return;
                default:
                    throw new NotImplementedException($"Not implemented MessageBoxResult '{messageBoxResult.ToString()}'");
            }
        }
        private async void TranslateToEnglish_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult messageBoxResult = MessageBox.Show("This will translate all the untranslated string to en-US.\nChoose 'Yes' if you agree.", "Translate Mode", MessageBoxButton.YesNo, MessageBoxImage.Question);

            switch (messageBoxResult)
            {
                case MessageBoxResult.Yes:
                    {
                        var progress = new Progress<int>(value =>
                        {
                            _progressBar.Value = value;
                            _progressPercentage.Text = value.ToString() + "%";
                        });
                        await Task.Run(() => TranslateToEnglish(progress));

                        break;
                    }
                case MessageBoxResult.No:
                    {
                        break;
                    }
                case MessageBoxResult.None:
                    return;
                default:
                    throw new NotImplementedException($"Not implemented MessageBoxResult '{messageBoxResult.ToString()}'");
            }
        }

        private void TranslateFile(IProgress<int> progress)
        {
            OpenFileDialog openFileDialog = CreateOpenFileDialog();
            openFileDialog.Title = "Import";
            bool? result = openFileDialog.ShowDialog();

            string filePath = openFileDialog.FileName;


            if (result == true)
            {
                this.Dispatcher.Invoke((Action)(() =>
                {
                    _progressPanel.Visibility = Visibility.Visible;
                    _progressBar.Value = 0;
                    _progressPercentage.Text = "0%";
                }));

                ObservableCollection<TranslationUnit> newTranslationUnits = XliffParser.GetTranslationUnitsFromFile(filePath);
                if (AreTranslationUnitsValid(newTranslationUnits))
                {
                    ObservableCollection<TranslationUnit> list = (ObservableCollection<TranslationUnit>)MainDataGrid.ItemsSource;

                    int count = list.Count;
                    int current = 0;

                    foreach (TranslationUnit originalTranslationUnit in list)
                    {
                        current++;

                        TranslationUnit translationUnit = newTranslationUnits.Where(otu => otu.Source == originalTranslationUnit.Source && otu.Target != "").FirstOrDefault();

                        if (translationUnit != null)
                        {
                            originalTranslationUnit.Description = translationUnit.Description;
                            originalTranslationUnit.Meaning = translationUnit.Meaning;
                            originalTranslationUnit.Target = translationUnit.Target;
                        }

                        var percentageComplete = (current * 100) / count;
                        progress.Report(percentageComplete);
                    }

                    MessageBox.Show("Translations has been successfully applied", "", MessageBoxButton.OK, MessageBoxImage.Information);

                    this.Dispatcher.Invoke((Action)(() =>
                    {
                        _progressPanel.Visibility = Visibility.Hidden;
                        MainDataGrid.Items.Refresh();
                    }));
                }
            }
        }

        private void TranslateToEnglish(IProgress<int> progress)
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                _progressPanel.Visibility = Visibility.Visible;
                _progressBar.Value = 0;
                _progressPercentage.Text = "0%";
            }));

            ObservableCollection<TranslationUnit> list = (ObservableCollection<TranslationUnit>)MainDataGrid.ItemsSource;

            int count = list.Count;
            int current = 0;

            foreach (TranslationUnit originalTranslationUnit in list)
            {
                current++;

                if (originalTranslationUnit != null && originalTranslationUnit.Target == "")
                {
                    originalTranslationUnit.Target = originalTranslationUnit.Source;
                }

                var percentageComplete = (current * 100) / count;
                progress.Report(percentageComplete);
            }

            MessageBox.Show("Translations has been successfully completed", "", MessageBoxButton.OK, MessageBoxImage.Information);

            this.Dispatcher.Invoke((Action)(() =>
            {
                _progressPanel.Visibility = Visibility.Hidden;
                MainDataGrid.Items.Refresh();
            }));
        }

        private void ExportMenuOption_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = e.Source as MenuItem;
            SaveAs(mi.Name,"");

        }

        private string GetLanguageCode() {
            MyPopupWindow popup = new MyPopupWindow();
            popup.ShowDialog();
            return(popup.LangCode);
        }
    }
}
