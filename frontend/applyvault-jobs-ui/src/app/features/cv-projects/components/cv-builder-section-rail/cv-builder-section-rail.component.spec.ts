import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CvBuilderSectionRailComponent } from './cv-builder-section-rail.component';

describe('CvBuilderSectionRailComponent', () => {
  let fixture: ComponentFixture<CvBuilderSectionRailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CvBuilderSectionRailComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(CvBuilderSectionRailComponent);
    fixture.componentRef.setInput('items', [
      { id: 'sec-contact', heading: 'Contact', complete: true, canToggle: false, included: true },
      { id: 'sec-summary', heading: 'Summary', complete: false, canToggle: true, included: true }
    ]);
    fixture.detectChanges();
  });

  it('lists fixed sections with completeness and emits jump', () => {
    const jump = jasmine.createSpy('jumpToSection');
    fixture.componentInstance.jumpToSection.subscribe(jump);

    const root = fixture.nativeElement as HTMLElement;
    const names = Array.from(root.querySelectorAll<HTMLButtonElement>('.cv-builder-rail__item'));
    expect(names.map((button) => (button.textContent ?? '').trim())).toEqual(['Contact', 'Summary']);
    expect(names[0].getAttribute('aria-label')).toBe('Contact, complete');
    expect(names[1].getAttribute('aria-label')).toBe('Summary, not filled, on CV');
    expect(root.querySelector('[aria-label="Omit Contact from CV"]')).toBeNull();

    names[1].click();
    expect(jump).toHaveBeenCalledWith('sec-summary');
  });

  it('marks the section in view as current', () => {
    fixture.componentRef.setInput('activeId', 'sec-summary');
    fixture.detectChanges();

    const buttons = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.cv-builder-rail__item')
    );
    expect(buttons[0].classList.contains('cv-builder-rail__item--active')).toBeFalse();
    expect(buttons[0].getAttribute('aria-current')).toBeNull();
    expect(buttons[1].classList.contains('cv-builder-rail__item--active')).toBeTrue();
    expect(buttons[1].getAttribute('aria-current')).toBe('location');
  });

  it('emits off for a section and leaves Contact without a switch', () => {
    const visibility = jasmine.createSpy('sectionVisibility');
    fixture.componentInstance.sectionVisibility.subscribe(visibility);

    const root = fixture.nativeElement as HTMLElement;
    const summary = root.querySelector('[aria-label="Omit Summary from CV"]') as HTMLButtonElement;
    expect(summary.textContent?.trim()).toBe('On');
    summary.click();
    expect(visibility).toHaveBeenCalledWith({ id: 'sec-summary', included: false });
  });
});
