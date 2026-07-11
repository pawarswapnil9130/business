import { Component } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styles: []
})
export class LoginComponent {
  isLoginMode = true;
  username = '';
  password = '';
  confirmPassword = '';
  errorMessage = '';
  successMessage = '';
  loading = false;
  returnUrl = '/';

  constructor(
    private apiService: ApiService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    // redirect to home if already logged in
    if (this.apiService.isAuthenticated()) {
      this.router.navigate(['/']);
    }
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/';
  }

  toggleMode() {
    this.isLoginMode = !this.isLoginMode;
    this.errorMessage = '';
    this.successMessage = '';
    this.username = '';
    this.password = '';
    this.confirmPassword = '';
  }

  onSubmit() {
    if (!this.username || !this.password) {
      this.errorMessage = 'Please fill in all fields.';
      return;
    }

    this.errorMessage = '';
    this.successMessage = '';
    this.loading = true;

    if (this.isLoginMode) {
      this.apiService.signin({ username: this.username, password: this.password }).subscribe({
        next: () => {
          this.router.navigateByUrl(this.returnUrl);
        },
        error: (err) => {
          this.loading = false;
          this.errorMessage = err.error?.message || 'Login failed. Please check credentials.';
        }
      });
    } else {
      if (this.password !== this.confirmPassword) {
        this.loading = false;
        this.errorMessage = 'Passwords do not match.';
        return;
      }

      this.apiService.signup({ username: this.username, password: this.password }).subscribe({
        next: (res) => {
          this.loading = false;
          this.successMessage = res.message || 'Registration successful! Wait for Super Admin approval.';
          this.isLoginMode = true;
          this.password = '';
          this.confirmPassword = '';
        },
        error: (err) => {
          this.loading = false;
          this.errorMessage = err.error?.message || 'Registration failed. Try a different username.';
        }
      });
    }
  }
}
