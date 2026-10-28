import { currentLanguage, loadLanguage, t } from './i18n';

describe('t', () => {
  afterEach(() => localStorage.removeItem('workshop.language'));

  it('defaults to English and fills placeholders', async () => {
    await loadLanguage();

    expect(currentLanguage()).toBe('en');
    expect(t('workOrder.started', { number: 'WO-00001' })).toBe('Started WO-00001.');
  });

  it('picks the plural form from count', async () => {
    await loadLanguage();

    expect(t('due.daysLate', { count: 1 })).toBe('1 day late');
    expect(t('due.daysLate', { count: 3 })).toBe('3 days late');
  });

  it('uses the saved language, with its own plural rules', async () => {
    localStorage.setItem('workshop.language', 'fr');
    await loadLanguage();

    expect(currentLanguage()).toBe('fr');
    // French counts 0 as singular, English does not ("0 orders").
    expect(t('salesOrders.count', { count: 0 })).toBe('0 commande');
    expect(t('salesOrders.count', { count: 3 })).toBe('3 commandes');
  });

  it('falls back to English for Russian, which was removed', async () => {
    localStorage.setItem('workshop.language', 'ru');
    await loadLanguage();

    expect(currentLanguage()).toBe('en');
  });

  it('falls back to English for an unknown saved language', async () => {
    localStorage.setItem('workshop.language', 'xx');
    await loadLanguage();

    expect(currentLanguage()).toBe('en');
  });
});
