import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styles: []
})
export class DashboardComponent implements OnInit {
  currentTab = 'dashboard'; // 'dashboard', 'products', 'manufacturing', 'trading', 'inventory', 'sales', 'reports', 'users'
  currentUser: any = null;
  isMobileMenuOpen = false;

  toggleMobileMenu() {
    this.isMobileMenuOpen = !this.isMobileMenuOpen;
  }

  closeMobileMenu() {
    this.isMobileMenuOpen = false;
  }

  // Global messages
  errorMessage = '';
  successMessage = '';

  // Data Arrays
  products: any[] = [];
  fabrics: any[] = [];
  batches: any[] = [];
  suppliers: any[] = [];
  purchases: any[] = [];
  stocks: any[] = [];
  salesOrders: any[] = [];
  profitReports: any[] = [];
  users: any[] = [];
  stockSearchQuery = '';

  // Metrics
  metrics = {
    totalSales: 0,
    totalProfit: 0,
    mfgSales: 0,
    mfgProfit: 0,
    tradingSales: 0,
    tradingProfit: 0,
    lowStockProductsCount: 0,
    activeProductionBatchesCount: 0
  };

  // Forms Models
  // Product Form
  newProduct = {
    name: '',
    category: '',
    productType: 'MANUFACTURED',
    designBrand: '',
    size: '',
    color: '',
    sellingPrice: 0,
    gstPercent: 12.00
  };

  // Fabric Form
  newFabric = {
    name: '',
    color: '',
    supplierId: null as number | null,
    costPerMeter: 0,
    totalMeters: 0
  };

  // Production Batch Form
  newBatch = {
    batchCode: '',
    fabricId: null as number | null,
    fabricMetersUsed: 0,
    wastageMeters: 0,
    productId: null as number | null,
    designName: '',
    quantityToSew: 0,
    fabricPerShirt: 1.30
  };

  // Complete Production Batch Form
  completionBatch = {
    batchId: 0,
    tailoringCost: 0,
    additionalCost: 0,
    quantityProduced: 0
  };
  showCompletionModal = false;

  // Supplier Form
  newSupplier = {
    name: '',
    contactNumber: '',
    email: '',
    address: ''
  };

  // Trading Purchase Form
  newPurchase = {
    invoiceNo: '',
    supplierId: null as number | null,
    productId: null as number | null,
    quantity: 0,
    purchasePrice: 0,
    gstPercent: 5.00
  };

  // User Onboarding Form
  newOnboardUser = {
    username: '',
    password: '',
    role: 'EMPLOYEE'
  };

  // Sales Order Form / Shopping Cart
  salesCart: any[] = [];
  customerName = '';
  customerPhone = '';
  cartProductId: number | null = null;
  cartItemType = 'PCS'; // 'PCS' or 'SET'
  cartQuantity = 1;
  cartUnitPrice = 0;
  cartDiscount = 0;

  constructor(private apiService: ApiService, private router: Router) {}

  ngOnInit() {
    this.currentUser = this.apiService.currentUserValue;
    if (!this.currentUser) {
      this.router.navigate(['/login']);
      return;
    }
    if (this.currentUser?.role === 'EMPLOYEE') {
      this.currentTab = 'sales';
    }
    this.loadTab(this.currentTab);
  }

  loadTab(tab: string) {
    this.closeMobileMenu();
    if (this.currentUser?.role === 'EMPLOYEE' && tab !== 'inventory' && tab !== 'sales') {
      tab = 'sales';
    }
    this.currentTab = tab;
    this.clearMessages();
    
    switch (tab) {
      case 'dashboard':
        this.fetchDashboardSummary();
        this.fetchStockSummary();
        break;
      case 'products':
        this.fetchProducts();
        break;
      case 'manufacturing':
        this.fetchFabrics();
        this.fetchBatches();
        this.fetchProducts(); // To select targets for production
        this.fetchSuppliers(); // Fabric suppliers
        break;
      case 'trading':
        this.fetchSuppliers();
        this.fetchPurchases();
        this.fetchProducts(); // To select targets for purchase
        break;
      case 'inventory':
        this.fetchStockSummary();
        break;
      case 'sales':
        this.fetchProducts();
        this.fetchStockSummary(); // To check stock balances on checkout
        this.fetchSalesOrders();
        break;
      case 'reports':
        this.fetchProfitReports();
        break;
      case 'users':
        if (this.apiService.isAdmin()) {
          this.fetchUsers();
        } else {
          this.currentTab = 'dashboard';
          this.fetchDashboardSummary();
        }
        break;
    }
  }

  signout() {
    this.apiService.signout();
    this.router.navigate(['/login']);
  }

  clearMessages() {
    this.errorMessage = '';
    this.successMessage = '';
  }

  // Helper properties
  get mfgProducts() {
    return this.products.filter(p => p.productType === 'MANUFACTURED');
  }

  get tradedProducts() {
    return this.products.filter(p => p.productType === 'TRADED');
  }

  get filteredStocks() {
    if (!this.stockSearchQuery) {
      return this.stocks;
    }
    const query = this.stockSearchQuery.toLowerCase().trim();
    return this.stocks.filter(s => 
      (s.productName && s.productName.toLowerCase().includes(query)) ||
      (s.category && s.category.toLowerCase().includes(query)) ||
      (s.designBrand && s.designBrand.toLowerCase().includes(query)) ||
      (s.size && s.size.toLowerCase().includes(query)) ||
      (s.color && s.color.toLowerCase().includes(query)) ||
      (s.productType && s.productType.toLowerCase().includes(query))
    );
  }

  // ==========================================
  // API FETCHES
  // ==========================================

  fetchDashboardSummary() {
    this.apiService.getDashboardSummary().subscribe({
      next: (data) => this.metrics = data,
      error: (err) => this.errorMessage = 'Failed to load dashboard metrics.'
    });
  }

  fetchProducts() {
    this.apiService.getProducts().subscribe({
      next: (data) => this.products = data,
      error: (err) => this.errorMessage = 'Failed to load products.'
    });
  }

  fetchFabrics() {
    this.apiService.getFabrics().subscribe({
      next: (data) => this.fabrics = data,
      error: (err) => this.errorMessage = 'Failed to load fabrics.'
    });
  }

  fetchBatches() {
    this.apiService.getBatches().subscribe({
      next: (data) => this.batches = data,
      error: (err) => this.errorMessage = 'Failed to load production batches.'
    });
  }

  fetchSuppliers() {
    this.apiService.getSuppliers().subscribe({
      next: (data) => this.suppliers = data,
      error: (err) => this.errorMessage = 'Failed to load suppliers.'
    });
  }

  fetchPurchases() {
    this.apiService.getPurchases().subscribe({
      next: (data) => this.purchases = data,
      error: (err) => this.errorMessage = 'Failed to load purchases.'
    });
  }

  fetchStockSummary() {
    this.apiService.getStockSummary().subscribe({
      next: (data) => this.stocks = data,
      error: (err) => this.errorMessage = 'Failed to load stock inventory.'
    });
  }

  onClearAllStock() {
    if (confirm('⚠️ WARNING: Are you absolutely sure you want to delete all stock ledger entries? This resets all clothing stock levels to 0. This action cannot be undone.')) {
      this.clearMessages();
      this.apiService.clearAllStock().subscribe({
        next: (res) => {
          this.successMessage = res.message || 'All stock ledger entries cleared successfully.';
          this.fetchDashboardSummary();
          this.fetchStockSummary();
        },
        error: (err) => {
          this.errorMessage = err.error?.message || 'Failed to clear stock ledger.';
        }
      });
    }
  }

  fetchSalesOrders() {
    this.apiService.getSalesOrders().subscribe({
      next: (data) => this.salesOrders = data,
      error: (err) => this.errorMessage = 'Failed to load sales orders.'
    });
  }

  onDeleteSalesOrder(id: number) {
    if (confirm('Are you sure you want to delete this sales invoice? This will revert the products back to stock.')) {
      this.clearMessages();
      this.apiService.deleteSalesOrder(id).subscribe({
        next: () => {
          this.successMessage = 'Sales invoice deleted successfully. Stock reverted.';
          this.fetchSalesOrders();
          this.fetchStockSummary();
          this.fetchDashboardSummary();
        },
        error: (err) => this.errorMessage = err.error?.message || 'Failed to delete sales invoice.'
      });
    }
  }

  downloadInvoice(order: any) {
    const doc = new jsPDF();
    
    // Header
    doc.setFontSize(22);
    doc.setTextColor(40, 40, 40);
    doc.text('Casa Enterprises', 105, 20, { align: 'center' });
    
    doc.setFontSize(10);
    doc.setTextColor(100, 100, 100);
    doc.text('Near bhekrainagar near pmpml road shop number 2', 105, 28, { align: 'center' });
    doc.text('hadapsar pune 28', 105, 33, { align: 'center' });
    
    // Line separator
    doc.setLineWidth(0.5);
    doc.line(14, 40, 196, 40);

    // Invoice Title
    doc.setFontSize(16);
    doc.setTextColor(0, 0, 0);
    doc.text('TAX INVOICE', 105, 50, { align: 'center' });
    
    // Details
    doc.setFontSize(11);
    doc.text(`Invoice No: ${order.invoiceNo}`, 14, 65);
    doc.text(`Date: ${new Date(order.salesDate).toLocaleDateString()}`, 14, 72);
    doc.text(`Customer Name: ${order.customerName}`, 14, 79);
    doc.text(`Customer Phone: ${order.customerPhone || 'N/A'}`, 14, 86);
    
    // Amounts section
    autoTable(doc, {
      startY: 100,
      head: [['Description', 'Amount (Rs.)']],
      body: [
        ['Net Value', order.totalAmount.toFixed(2)],
        ['GST (Tax)', order.totalGst.toFixed(2)],
      ],
      foot: [['Final Amount', order.finalAmount.toFixed(2)]],
      theme: 'grid',
      headStyles: { fillColor: [41, 128, 185] },
      footStyles: { fillColor: [240, 240, 240], textColor: [0, 0, 0], fontStyle: 'bold' }
    });

    // Footer
    const finalY = (doc as any).lastAutoTable.finalY || 150;
    doc.setFontSize(10);
    doc.text('Thank you for your business!', 105, finalY + 20, { align: 'center' });
    
    doc.save(`Invoice_${order.invoiceNo}.pdf`);
  }

  fetchProfitReports() {
    this.apiService.getProfitReport().subscribe({
      next: (data) => this.profitReports = data,
      error: (err) => this.errorMessage = 'Failed to load profit reports.'
    });
  }

  fetchUsers() {
    this.apiService.getUsers().subscribe({
      next: (data) => this.users = data,
      error: (err) => this.errorMessage = 'Failed to load user list.'
    });
  }

  // ==========================================
  // ACTIONS & SUBMISSIONS
  // ==========================================

  // Products
  onSubmitProduct() {
    this.clearMessages();
    this.apiService.createProduct(this.newProduct).subscribe({
      next: () => {
        this.successMessage = 'Product created successfully!';
        this.fetchProducts();
        // Reset form
        this.newProduct = {
          name: '',
          category: '',
          productType: 'MANUFACTURED',
          designBrand: '',
          size: '',
          color: '',
          sellingPrice: 0,
          gstPercent: 12.00
        };
      },
      error: (err) => this.errorMessage = err.error?.message || 'Failed to create product.'
    });
  }

  onDeleteProduct(id: number) {
    if (confirm('Are you sure you want to delete this product?')) {
      this.clearMessages();
      this.apiService.deleteProduct(id).subscribe({
        next: () => {
          this.successMessage = 'Product deleted successfully.';
          this.fetchProducts();
          this.fetchStockSummary();
          this.fetchDashboardSummary();
        },
        error: (err) => this.errorMessage = 'Cannot delete product; it might be referenced in production or purchases.'
      });
    }
  }

  // Fabric
  onSubmitFabric() {
    this.clearMessages();
    // Set supplierId to null if empty
    if (!this.newFabric.supplierId) {
      this.newFabric.supplierId = null;
    }
    this.apiService.createFabric(this.newFabric).subscribe({
      next: () => {
        this.successMessage = 'Fabric added successfully!';
        this.fetchFabrics();
        this.newFabric = { name: '', color: '', supplierId: null, costPerMeter: 0, totalMeters: 0 };
      },
      error: (err) => this.errorMessage = 'Failed to record fabric.'
    });
  }

  onDeleteFabric(id: number) {
    if (confirm('⚠️ WARNING: Are you sure you want to delete this fabric roll? This will also remove any associated production batches and their completed stocks.')) {
      this.clearMessages();
      this.apiService.deleteFabric(id).subscribe({
        next: () => {
          this.successMessage = 'Fabric roll and associated production batches deleted successfully.';
          this.fetchFabrics();
          this.fetchBatches();
          this.fetchStockSummary();
          this.fetchDashboardSummary();
        },
        error: (err) => this.errorMessage = err.error?.message || 'Failed to delete fabric.'
      });
    }
  }

  // Production Batches
  onSubmitBatch() {
    this.clearMessages();
    this.newBatch.fabricMetersUsed = this.newBatch.quantityToSew * this.newBatch.fabricPerShirt;
    this.apiService.startBatch(this.newBatch).subscribe({
      next: () => {
        this.successMessage = 'Production batch started!';
        this.fetchBatches();
        this.fetchFabrics(); // Reload fabric remaining totals
        this.newBatch = { batchCode: '', fabricId: null, fabricMetersUsed: 0, wastageMeters: 0, productId: null, designName: '', quantityToSew: 0, fabricPerShirt: 1.30 };
      },
      error: (err) => this.errorMessage = err.error?.message || 'Failed to start production batch.'
    });
  }

  openCompletionModal(batch: any) {
    this.completionBatch = {
      batchId: batch.id,
      tailoringCost: 0,
      additionalCost: 0,
      quantityProduced: 0
    };
    this.showCompletionModal = true;
  }

  onSubmitCompletion() {
    this.clearMessages();
    this.apiService.completeBatch(this.completionBatch).subscribe({
      next: () => {
        this.successMessage = 'Production run completed and shirts added to stock ledger!';
        this.showCompletionModal = false;
        this.fetchBatches();
        this.fetchStockSummary();
        this.fetchDashboardSummary();
        this.fetchProducts();
      },
      error: (err) => this.errorMessage = err.error?.message || 'Failed to complete production batch.'
    });
  }

  onDeleteBatch(id: number) {
    if (confirm('Are you sure you want to delete this production batch? This will also revert any finished stock added.')) {
      this.clearMessages();
      this.apiService.deleteBatch(id).subscribe({
        next: () => {
          this.successMessage = 'Production batch deleted successfully. Stock reverted.';
          this.fetchBatches();
          this.fetchFabrics();
          this.fetchStockSummary();
          this.fetchDashboardSummary();
        },
        error: (err) => this.errorMessage = err.error?.message || 'Failed to delete production batch.'
      });
    }
  }

  // Supplier
  onSubmitSupplier() {
    this.clearMessages();
    this.apiService.createSupplier(this.newSupplier).subscribe({
      next: () => {
        this.successMessage = 'Supplier registered successfully!';
        this.fetchSuppliers();
        this.newSupplier = { name: '', contactNumber: '', email: '', address: '' };
      },
      error: (err) => this.errorMessage = 'Failed to create supplier.'
    });
  }

  onDeleteSupplier(id: number) {
    if (confirm('⚠️ WARNING: Are you sure you want to delete this supplier? This will also remove all their purchase records and revert their purchased stock.')) {
      this.clearMessages();
      this.apiService.deleteSupplier(id).subscribe({
        next: () => {
          this.successMessage = 'Supplier and associated purchases deleted successfully.';
          this.fetchSuppliers();
          this.fetchPurchases();
          this.fetchFabrics();
          this.fetchStockSummary();
          this.fetchDashboardSummary();
        },
        error: (err) => this.errorMessage = err.error?.message || 'Failed to delete supplier.'
      });
    }
  }

  // Trading Purchase
  onPurchaseProductChange() {
    if (this.newPurchase.productId) {
      const prod = this.products.find(p => p.id == this.newPurchase.productId);
      if (prod) {
        this.newPurchase.purchasePrice = prod.costPrice || 0;
        this.newPurchase.gstPercent = prod.gstPercent || 12.00;
      }
    }
  }

  onSubmitPurchase() {
    this.clearMessages();
    this.apiService.recordPurchase(this.newPurchase).subscribe({
      next: () => {
        this.successMessage = 'Trading purchase recorded and items added to stock!';
        this.fetchPurchases();
        this.fetchStockSummary();
        this.fetchDashboardSummary();
        this.fetchProducts();
        this.newPurchase = { invoiceNo: '', supplierId: null, productId: null, quantity: 0, purchasePrice: 0, gstPercent: 12.00 };
      },
      error: (err) => this.errorMessage = err.error?.message || 'Failed to record purchase.'
    });
  }

  onDeletePurchase(id: number) {
    if (confirm('Are you sure you want to delete this ready-made purchase invoice? This will revert the purchased items from stock.')) {
      this.clearMessages();
      this.apiService.deletePurchase(id).subscribe({
        next: () => {
          this.successMessage = 'Trading purchase deleted successfully. Stock reverted.';
          this.fetchPurchases();
          this.fetchStockSummary();
          this.fetchDashboardSummary();
        },
        error: (err) => this.errorMessage = err.error?.message || 'Failed to delete purchase invoice.'
      });
    }
  }

  // ==========================================
  // SALES SHOPPING CART
  // ==========================================

  onCartProductChange() {
    if (this.cartProductId) {
      const prod = this.products.find(p => p.id == this.cartProductId);
      if (prod) {
        this.cartUnitPrice = prod.sellingPrice || 0;
      }
    }
  }

  addToCart() {
    if (!this.cartProductId) {
      alert('Please select a product.');
      return;
    }
    const prod = this.products.find(p => p.id == this.cartProductId);
    if (!prod) return;

    // Check duplicate
    const duplicate = this.salesCart.find(c => c.productId === this.cartProductId);
    if (duplicate) {
      duplicate.quantity += this.cartQuantity;
      duplicate.discount += this.cartDiscount;
      this.recalculateCartLine(duplicate);
    } else {
      const cartItem = {
        productId: prod.id,
        name: prod.name,
        size: prod.size,
        color: prod.color,
        itemType: 'PCS',
        quantity: this.cartQuantity,
        unitPrice: this.cartUnitPrice,
        discount: this.cartDiscount,
        gstPercent: prod.gstPercent,
        subTotal: 0
      };
      this.recalculateCartLine(cartItem);
      this.salesCart.push(cartItem);
    }

    // Reset item selector
    this.cartProductId = null;
    this.cartQuantity = 1;
    this.cartUnitPrice = 0;
    this.cartDiscount = 0;
  }

  recalculateCartLine(item: any) {
    const qty = item.quantity;
    const baseSub = (qty * item.unitPrice) - item.discount;
    const gstVal = baseSub * (item.gstPercent / 100.0);
    item.subTotal = baseSub + gstVal;
  }

  removeFromCart(index: number) {
    this.salesCart.splice(index, 1);
  }

  get cartPreTaxTotal() {
    return this.salesCart.reduce((sum, item) => sum + ((item.quantity * item.unitPrice) - item.discount), 0);
  }

  get cartTaxTotal() {
    return this.salesCart.reduce((sum, item) => {
      const baseSub = (item.quantity * item.unitPrice) - item.discount;
      return sum + (baseSub * (item.gstPercent / 100.0));
    }, 0);
  }

  get cartFinalTotal() {
    return this.salesCart.reduce((sum, item) => sum + item.subTotal, 0);
  }

  checkoutSalesOrder() {
    this.clearMessages();
    if (!this.customerName) {
      alert('Please enter a customer name.');
      return;
    }
    if (this.salesCart.length === 0) {
      alert('Your cart is empty.');
      return;
    }

    const orderDto = {
      customerName: this.customerName,
      customerPhone: this.customerPhone,
      items: this.salesCart.map(i => ({
        productId: i.productId,
        itemType: i.itemType,
        quantity: i.quantity,
        unitPrice: i.unitPrice,
        discount: i.discount
      }))
    };

    this.apiService.createSalesOrder(orderDto).subscribe({
      next: (res) => {
        this.successMessage = `Sales Invoice ${res.invoiceNo} successfully created and stock updated!`;
        this.salesCart = [];
        this.customerName = '';
        this.customerPhone = '';
        this.fetchSalesOrders();
        this.fetchStockSummary();
        this.fetchDashboardSummary();
        this.fetchProducts();
      },
      error: (err) => this.errorMessage = err.error?.message || 'Failed to place sales order.'
    });
  }

  // ==========================================
  // SUPER ADMIN USER ONBOARDING
  // ==========================================

  approveUser(user: any) {
    this.clearMessages();
    this.apiService.approveUser(user.id).subscribe({
      next: (res) => {
        this.successMessage = res.message;
        this.fetchUsers();
      },
      error: (err) => this.errorMessage = 'Failed to approve user.'
    });
  }

  removeUser(user: any) {
    if (confirm(`Are you sure you want to remove user: ${user.username}?`)) {
      this.clearMessages();
      this.apiService.removeUser(user.id).subscribe({
        next: (res) => {
          this.successMessage = res.message;
          this.fetchUsers();
        },
        error: (err) => this.errorMessage = err.error?.message || 'Failed to remove user.'
      });
    }
  }

  onSubmitOnboardUser() {
    this.clearMessages();
    this.apiService.onboardUser(this.newOnboardUser).subscribe({
      next: (res) => {
        this.successMessage = res.message || 'User onboarded successfully!';
        this.fetchUsers();
        this.newOnboardUser = {
          username: '',
          password: '',
          role: 'EMPLOYEE'
        };
      },
      error: (err) => this.errorMessage = err.error?.message || 'Failed to onboard user.'
    });
  }
}
