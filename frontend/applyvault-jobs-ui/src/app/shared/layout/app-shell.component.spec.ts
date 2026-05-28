import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { AppShellComponent } from './app-shell.component';
import { createAuthServiceMock, TEST_CURRENT_USER } from '../../../testing/auth-test-utils';

describe('AppShellComponent', () => {
  let fixture: ComponentFixture<AppShellComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppShellComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: createAuthServiceMock({
            authenticated: true,
            currentUser: TEST_CURRENT_USER
          })
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();
  });

  it('shows the signed-in user email in the shell', () => {
    const sessionText = fixture.nativeElement.querySelector('.app-shell__session')?.textContent ?? '';

    expect(sessionText).toContain(TEST_CURRENT_USER.email);
  });
});
