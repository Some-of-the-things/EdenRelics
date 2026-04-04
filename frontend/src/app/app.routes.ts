import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './guards/admin.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/home/home.component').then((m) => m.HomeComponent),
  },
  {
    path: 'product/:id',
    loadComponent: () =>
      import('./pages/product/product-page.component').then(
        (m) => m.ProductPageComponent
      ),
  },
  {
    path: 'cart',
    loadComponent: () =>
      import('./pages/cart/cart-page.component').then(
        (m) => m.CartPageComponent
      ),
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./pages/admin/admin-login.component').then(
        (m) => m.AdminLoginComponent
      ),
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./pages/register/register-page.component').then(
        (m) => m.RegisterPageComponent
      ),
  },
  {
    path: 'account',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/account/account-page.component').then(
        (m) => m.AccountPageComponent
      ),
  },
  {
    path: 'order-confirmation/:id',
    loadComponent: () =>
      import('./pages/order-confirmation/order-confirmation.component').then(
        (m) => m.OrderConfirmationComponent
      ),
  },
  {
    path: 'admin/login',
    redirectTo: 'login',
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./pages/admin/admin-page.component').then(
        (m) => m.AdminPageComponent
      ),
  },
  {
    path: 'privacy-policy',
    loadComponent: () =>
      import('./pages/policies/privacy-policy.component').then(
        (m) => m.PrivacyPolicyComponent
      ),
  },
  {
    path: 'modern-slavery-policy',
    loadComponent: () =>
      import('./pages/policies/modern-slavery-policy.component').then(
        (m) => m.ModernSlaveryPolicyComponent
      ),
  },
  {
    path: 'supply-chain-policy',
    loadComponent: () =>
      import('./pages/policies/supply-chain-policy.component').then(
        (m) => m.SupplyChainPolicyComponent
      ),
  },
  {
    path: 'returns-policy',
    loadComponent: () =>
      import('./pages/policies/returns-policy.component').then(
        (m) => m.ReturnsPolicyComponent
      ),
  },
  {
    path: 'security',
    loadComponent: () =>
      import('./pages/policies/security-report.component').then(
        (m) => m.SecurityReportComponent
      ),
  },
  {
    path: 'settings',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/settings/settings-page.component').then(
        (m) => m.SettingsPageComponent
      ),
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./pages/forgot-password/forgot-password.component').then(
        (m) => m.ForgotPasswordComponent
      ),
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./pages/reset-password/reset-password.component').then(
        (m) => m.ResetPasswordComponent
      ),
  },
  {
    path: 'verify-email',
    loadComponent: () =>
      import('./pages/verify-email/verify-email.component').then(
        (m) => m.VerifyEmailComponent
      ),
  },
  {
    path: 'blog',
    loadComponent: () =>
      import('./pages/blog/blog-list.component').then(
        (m) => m.BlogListComponent
      ),
  },
  {
    path: 'blog/:slug',
    loadComponent: () =>
      import('./pages/blog/blog-post.component').then(
        (m) => m.BlogPostComponent
      ),
  },
  {
    path: 'contact',
    loadComponent: () =>
      import('./pages/contact/contact-page.component').then(
        (m) => m.ContactPageComponent
      ),
  },
  { path: '**', redirectTo: '' },
];
