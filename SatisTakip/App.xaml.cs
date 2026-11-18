using System.Configuration;
using System.Data;
using System.Windows;

namespace SatisTakip;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	private void Application_Startup(object sender, StartupEventArgs e)
	{
		try
		{
			var login = new LoginWindow();
			if (login.ShowDialog() != true)
			{
				Shutdown();
				return;
			}

			UserSession.CurrentUser = login.User;
			MainWindow = new MainWindow();
			ShutdownMode = ShutdownMode.OnMainWindowClose;
			MainWindow.WindowState = WindowState.Maximized;
			MainWindow.Show();
		}
		catch (Exception exception)
		{
			MessageBox.Show($"Uygulama başlatılamadı.\n\n{exception.Message}", "Başlangıç hatası", MessageBoxButton.OK, MessageBoxImage.Error);
			Shutdown();
		}
	}
}

