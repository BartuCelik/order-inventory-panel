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
    localStorage.setItem('workshop.language', 'ru');
    await loadLanguage();

    expect(currentLanguage()).toBe('ru');
    expect(t('salesOrders.count', { count: 1 })).toBe('1 заказ');
    expect(t('salesOrders.count', { count: 3 })).toBe('3 заказа');
    expect(t('salesOrders.count', { count: 5 })).toBe('5 заказов');
  });

  it('falls back to English for an unknown saved language', async () => {
    localStorage.setItem('workshop.language', 'xx');
    await loadLanguage();

    expect(currentLanguage()).toBe('en');
  });
});
