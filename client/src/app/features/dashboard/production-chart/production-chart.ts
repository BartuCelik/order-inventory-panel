import { formatDate } from '@angular/common';
import {
  Component,
  DestroyRef,
  ElementRef,
  LOCALE_ID,
  afterRenderEffect,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { BarController, BarElement, CategoryScale, Chart, LinearScale, Tooltip } from 'chart.js';
import { ThemeService } from '../../../core/theme.service';
import { t } from '../../../core/i18n/i18n';
import { ProductionWeek } from '../dashboard.models';

Chart.register(BarController, BarElement, CategoryScale, LinearScale, Tooltip);

/** Completed work orders per week as bars, coloured from the theme tokens. */
@Component({
  selector: 'app-production-chart',
  template: `<canvas #canvas role="img" [attr.aria-label]="label()"></canvas>`,
  styles: `
    :host {
      display: block;
      position: relative;
      height: 220px;
      padding: 16px 20px 8px;
    }
  `,
})
export class ProductionChart {
  readonly weeks = input.required<ProductionWeek[]>();

  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private readonly theme = inject(ThemeService);
  private readonly locale = inject(LOCALE_ID);
  private chart?: Chart<'bar'>;

  protected label(): string {
    const total = this.weeks().reduce((sum, w) => sum + w.completed, 0);
    return t('dashboard.productionChartLabel', { count: total, weeks: this.weeks().length });
  }

  constructor() {
    // A system theme switch changes the tokens without touching the theme signal.
    const media = matchMedia('(prefers-color-scheme: dark)');
    const redraw = () => this.draw();
    media.addEventListener('change', redraw);
    inject(DestroyRef).onDestroy(() => {
      media.removeEventListener('change', redraw);
      this.chart?.destroy();
    });

    // After render, so the data-theme attribute and the tokens it selects are already applied.
    afterRenderEffect(() => {
      this.weeks();
      this.theme.choice();
      this.draw();
    });
  }

  private draw(): void {
    const canvas = this.canvas().nativeElement;
    const css = getComputedStyle(canvas);
    const token = (name: string) => css.getPropertyValue(name).trim();
    const weeks = this.weeks();
    const font = { family: token('--font'), size: 12 };

    this.chart?.destroy();
    this.chart = new Chart(canvas, {
      type: 'bar',
      data: {
        labels: weeks.map((w) => formatDate(w.weekStart, 'd MMM', this.locale)),
        datasets: [
          {
            data: weeks.map((w) => w.completed),
            backgroundColor: token('--accent'),
            hoverBackgroundColor: token('--accent-hover'),
            borderRadius: 6,
            maxBarThickness: 36,
          },
        ],
      },
      options: {
        maintainAspectRatio: false,
        animation: false,
        scales: {
          x: {
            grid: { display: false },
            border: { display: false },
            ticks: { color: token('--text-3'), font },
          },
          y: {
            beginAtZero: true,
            suggestedMax: 2,
            border: { display: false },
            grid: { color: token('--border-soft') },
            ticks: { color: token('--text-3'), font, precision: 0, maxTicksLimit: 4 },
          },
        },
        plugins: {
          tooltip: {
            displayColors: false,
            callbacks: {
              title: (items) =>
                t('dashboard.weekOf', {
                  date: formatDate(weeks[items[0].dataIndex].weekStart, 'd MMM', this.locale),
                }),
              label: (item) => t('dashboard.completedCount', { count: item.parsed.y ?? 0 }),
            },
          },
        },
      },
    });
  }
}
