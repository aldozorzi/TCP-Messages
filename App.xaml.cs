using Microsoft.Extensions.DependencyInjection;

namespace TCP_Messages
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Sottoscrizione all'evento (rimane qui)
            AppActions.OnAppAction += AppActions_OnAppAction;
        }

        // NUOVO MODO: Inizializza l'interfaccia qui
        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Se usi AppShell (consigliato in MAUI)
            return new Window(new AppShell());

            // Se invece usi ancora NavigationPage + MainPage:
            // return new Window(new NavigationPage(new MainPage()));
        }

        private void AppActions_OnAppAction(object? sender, AppActionEventArgs? e)
        {
            if (e == null) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // NUOVO MODO per accedere alla pagina radice senza usare la proprietà obsoleta
                var rootPage = Windows[0].Page;

                if (rootPage is NavigationPage navPage && navPage.CurrentPage is MainPage mainPage)
                {
                    mainPage.HandleExternalAction(e.AppAction.Id);
                }
                else if (rootPage is MainPage directPage)
                {
                    directPage.HandleExternalAction(e.AppAction.Id);
                }
                // Se usi AppShell, la logica cambia leggermente:
                else if (rootPage is Shell shell && shell.CurrentPage is MainPage shellPage)
                {
                    shellPage.HandleExternalAction(e.AppAction.Id);
                }
            });
        }
    }
}