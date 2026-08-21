import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { map } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private baseUrl = (typeof window !== 'undefined' && (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1'))
    ? (window.location.port === '4200' ? 'http://localhost:5000/api' : '/api')
    : 'https://business-backend-q18v.onrender.com/api';

  private currentUserSubject = new BehaviorSubject<any>(null);

  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(private http: HttpClient) {
    const savedUser = localStorage.getItem('currentUser');
    if (savedUser) {
      try {
        this.currentUserSubject.next(JSON.parse(savedUser));
      } catch (e) {
        localStorage.removeItem('currentUser');
      }
    }
  }

  public get currentUserValue() {
    return this.currentUserSubject.value;
  }

  private getHeaders(): HttpHeaders {
    let headers = new HttpHeaders({
      'Content-Type': 'application/json'
    });

    if (this.currentUserValue && this.currentUserValue.token) {
      headers = headers.set('Authorization', `Bearer ${this.currentUserValue.token}`);
    }

    return headers;
  }

  // ==========================================
  // AUTHENTICATION
  // ==========================================

  signin(dto: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/auth/signin`, dto).pipe(
      map(user => {
        if (user && user.token) {
          localStorage.setItem('currentUser', JSON.stringify(user));
          this.currentUserSubject.next(user);
        }
        return user;
      })
    );
  }

  signup(dto: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/auth/signup`, dto);
  }

  signout() {
    localStorage.removeItem('currentUser');
    this.currentUserSubject.next(null);
  }

  isAdmin(): boolean {
    return this.currentUserValue && (this.currentUserValue.role === 'SUPER_ADMIN' || this.currentUserValue.role === 'ADMIN');
  }

  isAuthenticated(): boolean {
    return !!this.currentUserValue;
  }

  // ==========================================
  // USER MANAGEMENT (SUPER ADMIN ONLY)
  // ==========================================

  getUsers(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/auth/users`, { headers: this.getHeaders() });
  }

  approveUser(id: number): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/auth/users/${id}/approve`, {}, { headers: this.getHeaders() });
  }

  removeUser(id: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/auth/users/${id}`, { headers: this.getHeaders() });
  }

  onboardUser(dto: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/auth/onboard`, dto, { headers: this.getHeaders() });
  }

  // ==========================================
  // PRODUCT MASTER
  // ==========================================

  getProducts(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/products`, { headers: this.getHeaders() });
  }

  createProduct(product: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/products`, product, { headers: this.getHeaders() });
  }

  updateProduct(id: number, product: any): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/products/${id}`, product, { headers: this.getHeaders() });
  }

  deleteProduct(id: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/products/${id}`, { headers: this.getHeaders() });
  }

  // ==========================================
  // MANUFACTURING (FABRIC & BATCHES)
  // ==========================================

  getFabrics(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/manufacturing/fabrics`, { headers: this.getHeaders() });
  }

  createFabric(fabric: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/manufacturing/fabrics`, fabric, { headers: this.getHeaders() });
  }

  deleteFabric(id: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/manufacturing/fabrics/${id}`, { headers: this.getHeaders() });
  }

  getBatches(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/manufacturing/batches`, { headers: this.getHeaders() });
  }

  startBatch(dto: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/manufacturing/batches/start`, dto, { headers: this.getHeaders() });
  }

  completeBatch(dto: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/manufacturing/batches/complete`, dto, { headers: this.getHeaders() });
  }

  deleteBatch(id: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/manufacturing/batches/${id}`, { headers: this.getHeaders() });
  }

  // ==========================================
  // TRADING (SUPPLIERS & PURCHASES)
  // ==========================================

  getSuppliers(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/trading/suppliers`, { headers: this.getHeaders() });
  }

  createSupplier(supplier: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/trading/suppliers`, supplier, { headers: this.getHeaders() });
  }

  deleteSupplier(id: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/trading/suppliers/${id}`, { headers: this.getHeaders() });
  }

  getPurchases(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/trading/purchases`, { headers: this.getHeaders() });
  }

  recordPurchase(dto: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/trading/purchases`, dto, { headers: this.getHeaders() });
  }

  deletePurchase(id: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/trading/purchases/${id}`, { headers: this.getHeaders() });
  }

  // ==========================================
  // UNIFIED INVENTORY
  // ==========================================

  getStockSummary(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/inventory/summary`, { headers: this.getHeaders() });
  }

  clearAllStock(): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/inventory/clear`, { headers: this.getHeaders() });
  }

  // ==========================================
  // SALES
  // ==========================================

  getSalesOrders(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/sales`, { headers: this.getHeaders() });
  }

  getSalesOrderById(id: number): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/sales/${id}`, { headers: this.getHeaders() });
  }

  createSalesOrder(dto: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/sales`, dto, { headers: this.getHeaders() });
  }

  deleteSalesOrder(id: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/sales/${id}`, { headers: this.getHeaders() });
  }

  // ==========================================
  // OPERATING EXPENSES
  // ==========================================

  getExpenses(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/expenses`, { headers: this.getHeaders() });
  }

  createExpense(dto: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/expenses`, dto, { headers: this.getHeaders() });
  }

  deleteExpense(id: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/expenses/${id}`, { headers: this.getHeaders() });
  }

  // ==========================================
  // REPORTS & DASHBOARD
  // ==========================================

  getProfitReport(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/reports/profit`, { headers: this.getHeaders() });
  }

  getDashboardSummary(): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/reports/dashboard`, { headers: this.getHeaders() });
  }

  // ==========================================
  // WHOLESALE CUSTOMERS & PORTAL ORDERS (ADMIN)
  // ==========================================

  getWholesaleCustomers(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/admin/customers`, { headers: this.getHeaders() });
  }

  updateCustomerStatus(id: number, status: string, notes?: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/admin/customers/${id}/status`, { status, notes }, { headers: this.getHeaders() });
  }

  updateCustomerType(id: number, customerType: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/admin/customers/${id}/type`, { customerType }, { headers: this.getHeaders() });
  }

  getPortalWholesaleOrders(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/admin/customers/orders`, { headers: this.getHeaders() });
  }

  updateOrderStatus(id: number, orderStatus: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/admin/customers/orders/${id}/status`, { orderStatus }, { headers: this.getHeaders() });
  }

  uploadProductImage(productId: number, file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    let headers = new HttpHeaders();
    if (this.currentUserValue && this.currentUserValue.token) {
      headers = headers.set('Authorization', `Bearer ${this.currentUserValue.token}`);
    }
    return this.http.post<any>(`${this.baseUrl}/products/${productId}/upload-image`, formData, { headers });
  }

  // ==========================================
  // SETTINGS
  // ==========================================

  getSettings(key: string): Observable<any> {
    return this.http.get(`${this.baseUrl}/settings/${key}`, { responseType: 'text' });
  }

  saveSettings(key: string, value: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/settings/${key}`, value, { headers: this.getHeaders() });
  }

  uploadQrCode(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    let headers = new HttpHeaders();
    if (this.currentUserValue && this.currentUserValue.token) {
      headers = headers.set('Authorization', `Bearer ${this.currentUserValue.token}`);
    }
    return this.http.post<any>(`${this.baseUrl}/settings/upload-qr`, formData, { headers: headers });
  }
}
