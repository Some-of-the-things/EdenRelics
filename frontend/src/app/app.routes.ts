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
    path: 'review/:orderId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/review/review-page.component').then(
        (m) => m.ReviewPageComponent
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
    path: 'terms-conditions',
    loadComponent: () =>
      import('./pages/policies/terms-conditions.component').then(
        (m) => m.TermsConditionsComponent
      ),
  },
  {
    path: 'cookie-policy',
    loadComponent: () =>
      import('./pages/policies/cookie-policy.component').then(
        (m) => m.CookiePolicyComponent
      ),
  },
  {
    path: 'accessibility-report',
    loadComponent: () =>
      import('./pages/policies/accessibility-report.component').then(
        (m) => m.AccessibilityReportComponent
      ),
  },
  {
    path: 'compliance-report',
    loadComponent: () =>
      import('./pages/policies/compliance-report.component').then(
        (m) => m.ComplianceReportComponent
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
    path: 'designers',
    loadComponent: () =>
      import('./pages/designers/designers-index.component').then(
        (m) => m.DesignersIndexComponent
      ),
  },
  {
    path: 'designers/:slug',
    loadComponent: () =>
      import('./pages/designers/designer-page.component').then(
        (m) => m.DesignerPageComponent
      ),
  },
  {
    path: 'collections/preview/:slug',
    data: { preview: true },
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./pages/collections/collection-preview.component').then(
        (m) => m.CollectionPreviewComponent
      ),
  },
  {
    path: 'collections/:slug',
    loadComponent: () =>
      import('./pages/collections/collection-page.component').then(
        (m) => m.CollectionPageComponent
      ),
  },
  {
    path: 'blog/preview/:slug',
    data: { preview: true },
    loadComponent: () =>
      import('./pages/blog/blog-post.component').then(
        (m) => m.BlogPostComponent
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
    path: 'care',
    loadComponent: () =>
      import('./pages/care/care-hub.component').then(
        (m) => m.CareHubComponent
      ),
  },
  {
    path: 'care/fabric/:slug',
    loadComponent: () =>
      import('./pages/care/care-fabric.component').then(
        (m) => m.CareFabricComponent
      ),
  },
  {
    path: 'care/problem/:slug',
    loadComponent: () =>
      import('./pages/care/care-issue.component').then(
        (m) => m.CareIssueComponent
      ),
  },
  {
    path: 'contact',
    loadComponent: () =>
      import('./pages/contact/contact-page.component').then(
        (m) => m.ContactPageComponent
      ),
  },
  {
    path: 'about',
    loadComponent: () =>
      import('./pages/about/about.component').then(
        (m) => m.AboutComponent
      ),
  },
  {
    path: '**',
    loadComponent: () =>
      import('./pages/not-found/not-found.component').then(
        (m) => m.NotFoundComponent
      ),
  },
];
