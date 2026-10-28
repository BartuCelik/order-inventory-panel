import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { loadLanguage, t } from '../../core/i18n/i18n';
import { Shell } from './shell';

describe('Shell', () => {
  beforeEach(async () => {
    await loadLanguage();
    await TestBed.configureTestingModule({
      imports: [Shell],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  function render(): HTMLElement {
    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the six pages as links in the top bar', () => {
    const links = [...render().querySelectorAll('header.topbar nav a')];

    expect(links.map((a) => a.getAttribute('href'))).toEqual([
      '/dashboard',
      '/work-orders',
      '/sales-orders',
      '/customers',
      '/products',
      '/stock',
    ]);
  });

  it('has no side drawer', () => {
    expect(render().querySelector('mat-sidenav')).toBeNull();
  });

  it('shows the demo note in the footer', () => {
    expect(render().querySelector('footer.demo-note')?.textContent).toContain(t('shell.demoNote'));
  });
});
