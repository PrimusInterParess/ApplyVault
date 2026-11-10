import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CvUnplacedDrawerComponent } from './cv-unplaced-drawer.component';

describe('CvUnplacedDrawerComponent', () => {
  let fixture: ComponentFixture<CvUnplacedDrawerComponent>;
  let writeText: jasmine.Spy;

  beforeEach(async () => {
    writeText = jasmine.createSpy('writeText').and.returnValue(Promise.resolve());
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText }
    });

    await TestBed.configureTestingModule({
      imports: [CvUnplacedDrawerComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(CvUnplacedDrawerComponent);
    fixture.componentRef.setInput('open', true);
    fixture.componentRef.setInput('items', ['Languages: French, German', 'Volunteer: Habitat']);
    fixture.detectChanges();
  });

  it('lists leftover import text with copy and dismiss actions', () => {
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('#cv-builder-unplaced-drawer')).not.toBeNull();
    expect(root.textContent).toContain('Unplaced (2)');
    expect(root.textContent).toContain('Languages: French, German');
    expect(root.textContent).toContain('not exported');

    const copy = Array.from(root.querySelectorAll('button')).find(
      (button) => (button.textContent ?? '').trim() === 'Copy'
    ) as HTMLButtonElement;
    copy.click();
    expect(writeText).toHaveBeenCalledWith('Languages: French, German');
  });

  it('emits dismiss for one item and dismiss all', () => {
    const dismissSpy = jasmine.createSpy('dismissItem');
    const dismissAllSpy = jasmine.createSpy('dismissAll');
    fixture.componentInstance.dismissItem.subscribe(dismissSpy);
    fixture.componentInstance.dismissAll.subscribe(dismissAllSpy);

    const root = fixture.nativeElement as HTMLElement;
    const dismiss = Array.from(root.querySelectorAll('button')).find(
      (button) => (button.textContent ?? '').trim() === 'Dismiss'
    ) as HTMLButtonElement;
    dismiss.click();
    expect(dismissSpy).toHaveBeenCalledWith(0);

    const dismissAll = Array.from(root.querySelectorAll('button')).find(
      (button) => (button.textContent ?? '').trim() === 'Dismiss all'
    ) as HTMLButtonElement;
    dismissAll.click();
    expect(dismissAllSpy).toHaveBeenCalled();
  });
});
