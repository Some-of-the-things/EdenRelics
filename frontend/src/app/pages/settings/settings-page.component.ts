import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService, AccountProfileDto, AddressDto } from '../../services/auth.service';
import { PasskeyService, PasskeyInfo } from '../../services/passkey.service';

type Section = 'name' | 'delivery' | 'billing' | 'payment' | 'password' | 'mfa' | 'passkeys';

@Component({
  selector: 'app-settings-page',
  imports: [FormsModule, RouterLink, DatePipe],
  templateUrl: './settings-page.component.html',
  styleUrl: './settings-page.component.scss',
})
export class SettingsPageComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly passkeyService = inject(PasskeyService);

  readonly active = signal<Section | null>(null);
  readonly saving = signal(false);
  readonly message = signal('');
  readonly isError = signal(false);

  // Name
  firstName = '';
  lastName = '';

  // Delivery address
  deliveryLine1 = '';
  deliveryLine2 = '';
  deliveryCity = '';
  deliveryCounty = '';
  deliveryPostcode = '';
  deliveryCountry = '';

  // Billing address
  billingLine1 = '';
  billingLine2 = '';
  billingCity = '';
  billingCounty = '';
  billingPostcode = '';
  billingCountry = '';

  // Payment
  cardholderName = '';
  cardLast4 = '';
  cardBrand = '';
  expiryMonth: number | null = null;
  expiryYear: number | null = null;

  // Password
  currentPassword = '';
  newPassword = '';
  confirmNewPassword = '';

  // MFA
  readonly mfaEnabled = signal(false);
  readonly mfaSetupSecret = signal('');
  readonly mfaQrUrl = signal('');
  mfaSetupCode = '';
  mfaDisableCode = '';

  // Passkeys
  readonly passkeySupported = signal(false);
  readonly passkeys = signal<PasskeyInfo[]>([]);

  ngOnInit(): void {
    this.auth.getProfile().subscribe({
      next: (p) => this.populateForm(p),
      error: () => {},
    });
    this.passkeySupported.set(this.passkeyService.supportsPasskeys());
    if (this.passkeySupported()) {
      this.loadPasskeys();
    }
  }

  private loadPasskeys(): void {
    this.passkeyService.getCredentials().subscribe({
      next: (creds) => this.passkeys.set(creds),
      error: () => {},
    });
  }

  toggle(section: Section): void {
    this.message.set('');
    this.active.set(this.active() === section ? null : section);
  }

  saveName(): void {
    this.saving.set(true);
    this.message.set('');
    this.auth.updateProfile(this.firstName, this.lastName).subscribe({
      next: (p) => {
        this.populateForm(p);
        this.showSuccess('Name updated.');
      },
      error: () => this.showError('Failed to update name.'),
    });
  }

  saveDeliveryAddress(): void {
    this.saving.set(true);
    this.message.set('');
    this.auth.updateDeliveryAddress({
      addressLine1: this.deliveryLine1 || null,
      addressLine2: this.deliveryLine2 || null,
      city: this.deliveryCity || null,
      county: this.deliveryCounty || null,
      postcode: this.deliveryPostcode || null,
      country: this.deliveryCountry || null,
    }).subscribe({
      next: (p) => {
        this.populateForm(p);
        this.showSuccess('Delivery address updated.');
      },
      error: () => this.showError('Failed to update delivery address.'),
    });
  }

  saveBillingAddress(): void {
    this.saving.set(true);
    this.message.set('');
    this.auth.updateBillingAddress({
      addressLine1: this.billingLine1 || null,
      addressLine2: this.billingLine2 || null,
      city: this.billingCity || null,
      county: this.billingCounty || null,
      postcode: this.billingPostcode || null,
      country: this.billingCountry || null,
    }).subscribe({
      next: (p) => {
        this.populateForm(p);
        this.showSuccess('Billing address updated.');
      },
      error: () => this.showError('Failed to update billing address.'),
    });
  }

  savePayment(): void {
    this.saving.set(true);
    this.message.set('');
    this.auth.updatePayment({
      cardholderName: this.cardholderName,
      cardLast4: this.cardLast4,
      cardBrand: this.cardBrand,
      expiryMonth: this.expiryMonth ?? 0,
      expiryYear: this.expiryYear ?? 0,
    }).subscribe({
      next: (p) => {
        this.populateForm(p);
        this.showSuccess('Payment details updated.');
      },
      error: () => this.showError('Failed to update payment details.'),
    });
  }

  changePassword(): void {
    if (this.newPassword !== this.confirmNewPassword) {
      this.isError.set(true);
      this.message.set('Passwords do not match.');
      return;
    }
    this.saving.set(true);
    this.message.set('');
    this.auth.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: () => {
        this.currentPassword = '';
        this.newPassword = '';
        this.confirmNewPassword = '';
        this.showSuccess('Password changed successfully.');
      },
      error: (err) => this.showError(err.error?.message ?? 'Failed to change password.'),
    });
  }

  addPasskey(): void {
    this.saving.set(true);
    this.message.set('');
    this.passkeyService.registerPasskey().subscribe({
      next: () => {
        this.loadPasskeys();
        this.showSuccess('Passkey added successfully.');
      },
      error: () => this.showError('Failed to register passkey.'),
    });
  }

  removePasskey(id: string): void {
    this.passkeyService.deleteCredential(id).subscribe({
      next: () => {
        this.passkeys.set(this.passkeys().filter(p => p.id !== id));
        this.showSuccess('Passkey removed.');
      },
      error: () => this.showError('Failed to remove passkey.'),
    });
  }

  startMfaSetup(): void {
    this.saving.set(true);
    this.message.set('');
    this.auth.setupMfa().subscribe({
      next: (res) => {
        this.saving.set(false);
        this.mfaSetupSecret.set(res.secret);
        this.mfaQrUrl.set(`https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(res.qrUri)}`);
      },
      error: (err) => this.showError(err.error?.message ?? 'Failed to set up MFA.'),
    });
  }

  confirmMfaSetup(): void {
    this.saving.set(true);
    this.message.set('');
    this.auth.verifyMfaSetup(this.mfaSetupCode).subscribe({
      next: () => {
        this.mfaEnabled.set(true);
        this.mfaSetupSecret.set('');
        this.mfaQrUrl.set('');
        this.mfaSetupCode = '';
        this.showSuccess('Two-factor authentication has been enabled.');
      },
      error: (err) => this.showError(err.error?.message ?? 'Invalid code.'),
    });
  }

  disableMfa(): void {
    this.saving.set(true);
    this.message.set('');
    this.auth.disableMfa(this.mfaDisableCode).subscribe({
      next: () => {
        this.mfaEnabled.set(false);
        this.mfaDisableCode = '';
        this.showSuccess('Two-factor authentication has been disabled.');
      },
      error: (err) => this.showError(err.error?.message ?? 'Invalid code.'),
    });
  }

  private populateForm(p: AccountProfileDto): void {
    this.firstName = p.firstName;
    this.lastName = p.lastName;

    this.deliveryLine1 = p.deliveryAddress.addressLine1 ?? '';
    this.deliveryLine2 = p.deliveryAddress.addressLine2 ?? '';
    this.deliveryCity = p.deliveryAddress.city ?? '';
    this.deliveryCounty = p.deliveryAddress.county ?? '';
    this.deliveryPostcode = p.deliveryAddress.postcode ?? '';
    this.deliveryCountry = p.deliveryAddress.country ?? '';

    this.billingLine1 = p.billingAddress.addressLine1 ?? '';
    this.billingLine2 = p.billingAddress.addressLine2 ?? '';
    this.billingCity = p.billingAddress.city ?? '';
    this.billingCounty = p.billingAddress.county ?? '';
    this.billingPostcode = p.billingAddress.postcode ?? '';
    this.billingCountry = p.billingAddress.country ?? '';

    if (p.payment) {
      this.cardholderName = p.payment.cardholderName ?? '';
      this.cardLast4 = p.payment.cardLast4 ?? '';
      this.cardBrand = p.payment.cardBrand ?? '';
      this.expiryMonth = p.payment.expiryMonth;
      this.expiryYear = p.payment.expiryYear;
    }

    this.mfaEnabled.set(p.mfaEnabled);
  }

  private showSuccess(msg: string): void {
    this.saving.set(false);
    this.isError.set(false);
    this.message.set(msg);
  }

  private showError(msg: string): void {
    this.saving.set(false);
    this.isError.set(true);
    this.message.set(msg);
  }
}
