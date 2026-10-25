import { loadLanguage } from './app/core/i18n/i18n';

// Translations first, then the app: app modules build labels at load time, so they must find the language ready.
loadLanguage()
  .then(async () => {
    const [{ bootstrapApplication }, { appConfig }, { App }] = await Promise.all([
      import('@angular/platform-browser'),
      import('./app/app.config'),
      import('./app/app'),
    ]);
    await bootstrapApplication(App, appConfig);
  })
  .catch((err) => console.error(err));
