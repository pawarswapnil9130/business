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
  expenses: any[] = [];
  users: any[] = [];
  wholesaleCustomers: any[] = [];
  wholesaleOrders: any[] = [];
  customerSearchQuery = '';
  orderStatusFilter = 'ALL';
  stockSearchQuery = '';
  expenseSearchQuery = '';

  // Overview Interactive Monthly Drilldown
  activeDrilldown: 'none' | 'sales' | 'profit' | 'manufacturing' | 'trading' = 'none';

  // Metrics
  metrics = {
    totalSales: 0,
    totalProfit: 0,
    grossProfit: 0,
    totalExpenses: 0,
    mfgSales: 0,
    mfgProfit: 0,
    tradingSales: 0,
    tradingProfit: 0,
    lowStockProductsCount: 0,
    activeProductionBatchesCount: 0
  };

  // Searchable Product Dropdown for POS Desk
  itemSearchQuery = '';
  isItemDropdownOpen = false;
  selectedProduct: any = null;

  // Operating Expenses
  newExpense = {
    title: '',
    category: 'Rent',
    amount: 0,
    expenseDate: new Date().toISOString().substring(0, 10),
    paymentMode: 'CASH',
    notes: ''
  };

  expenseCategories = [
    'Rent',
    'Electricity / Utilities',
    'Fuel & Transport',
    'Salaries & Wages',
    'Tea & Refreshments',
    'Maintenance & Repairs',
    'Packaging & Stationery',
    'Marketing & Promotion',
    'Miscellaneous'
  ];

  // ==========================================
  // STORE & BANK PROFILE CONFIGURATION
  // (Editable from the UI Settings Tab)
  // ==========================================
  companyProfile = {
    name: 'CASA ENTERPRISES',
    tagline: 'Apparel Manufacturing & Wholesale Trading',
    address: 'Near Bhekrainagar, Near PMPML Road, Shop No. 2, Hadapsar, Pune - 411028',
    phone: '+91 98765 43210',
    email: 'contact@casaenterprises.com',
    gstin: '27AABCC1234D1Z5',
    state: 'Maharashtra (Code 27)',
    // Bank & UPI Details
    bankName: 'HDFC Bank',
    accountName: 'Casa Enterprises',
    accountNumber: '50200012345678',
    ifscCode: 'HDFC0001234',
    upiId: 'casaenterprises@upi',
    gpayPhone: '+91 9876543210',
    // Terms & Conditions on Invoice
    termsText: '1. Goods once sold will not be exchanged or returned without original bill.\n2. Please check sizes and items at the time of delivery.\n3. All disputes are subject to Pune jurisdiction only.',
    terms: [
      '1. Goods once sold will not be exchanged or returned without original bill.',
      '2. Please check sizes and items at the time of delivery.',
      '3. All disputes are subject to Pune jurisdiction only.'
    ]
  };

  loadCompanyProfile() {
    const saved = localStorage.getItem('apparel_company_profile');
    if (saved) {
      try {
        const parsed = JSON.parse(saved);
        this.companyProfile = { ...this.companyProfile, ...parsed };
        if (this.companyProfile.termsText) {
          this.companyProfile.terms = this.companyProfile.termsText.split('\n').filter(t => t.trim().length > 0);
        }
      } catch (e) {
        console.error('Failed to parse saved company profile:', e);
      }
    }
  }

  saveCompanyProfile() {
    if (this.companyProfile.termsText) {
      this.companyProfile.terms = this.companyProfile.termsText.split('\n').filter(t => t.trim().length > 0);
    }
    localStorage.setItem('apparel_company_profile', JSON.stringify(this.companyProfile));
    this.successMessage = 'Store profile & bank details saved successfully! All future invoice downloads will use these details.';
    setTimeout(() => this.successMessage = '', 5000);
  }

  resetCompanyProfile() {
    this.companyProfile = {
      name: 'CASA ENTERPRISES',
      tagline: 'Apparel Manufacturing & Wholesale Trading',
      address: 'Near Bhekrainagar, Near PMPML Road, Shop No. 2, Hadapsar, Pune - 411028',
      phone: '+91 98765 43210',
      email: 'contact@casaenterprises.com',
      gstin: '27AABCC1234D1Z5',
      state: 'Maharashtra (Code 27)',
      bankName: 'HDFC Bank',
      accountName: 'Casa Enterprises',
      accountNumber: '50200012345678',
      ifscCode: 'HDFC0001234',
      upiId: 'casaenterprises@upi',
      gpayPhone: '+91 9876543210',
      termsText: '1. Goods once sold will not be exchanged or returned without original bill.\n2. Please check sizes and items at the time of delivery.\n3. All disputes are subject to Pune jurisdiction only.',
      terms: [
        '1. Goods once sold will not be exchanged or returned without original bill.',
        '2. Please check sizes and items at the time of delivery.',
        '3. All disputes are subject to Pune jurisdiction only.'
      ]
    };
    localStorage.removeItem('apparel_company_profile');
    this.successMessage = 'Store profile reset to original defaults.';
    setTimeout(() => this.successMessage = '', 4000);
  }

  previewInvoice() {
    // Generate a sample preview invoice with current profile
    const sampleOrder = {
      id: 9999,
      invoiceNo: 'PREVIEW-INV-001',
      customerName: 'Sample Customer (Preview)',
      customerPhone: '+91 98989 00000',
      salesDate: new Date().toISOString(),
      totalAmount: 1800.00,
      totalGst: 216.00,
      finalAmount: 2016.00,
      items: [
        {
          productId: 1,
          productName: 'Premium Cotton Formal Shirt',
          itemType: 'PCS',
          quantity: 2,
          unitPrice: 900.00,
          discount: 0,
          gstPercent: 12.00,
          subTotal: 2016.00,
          product: {
            name: 'Premium Cotton Formal Shirt',
            category: 'Formal Shirts',
            size: 'L (40)',
            color: 'Sky Blue',
            designBrand: 'Casa Classic',
            gstPercent: 12.00
          }
        }
      ]
    };
    this.generateInvoicePdf(sampleOrder);
  }

  // Forms Models
  // Product Form
  newProduct = {
    name: '',
    category: 'Shirts',
    productType: 'MANUFACTURED',
    designBrand: '',
    size: '38, 40, 42, 44',
    color: '',
    sellingPrice: 0,
    distributorPrice: 0,
    setSize: 4, // 3 pcs or 4 pcs per set
    setRatio: '38, 40, 42, 44',
    gstPercent: 12.00,
    imageUrl: ''
  };
  selectedProductImageFile: File | null = null;
  productImagePreview: string | null = null;

  onProductImageSelected(event: any) {
    const file = event.target.files[0];
    if (file) {
      this.selectedProductImageFile = file;
      const reader = new FileReader();
      reader.onload = () => {
        this.productImagePreview = reader.result as string;
      };
      reader.readAsDataURL(file);
    }
  }

  clearProductImageSelection() {
    this.selectedProductImageFile = null;
    this.productImagePreview = null;
  }

  onUploadBatchPhoto(batch: any, event: any) {
    const file = event.target.files[0];
    if (file && batch) {
      const prodId = batch.productId || batch.product?.id;
      if (prodId) {
        this.clearMessages();
        this.apiService.uploadProductImage(prodId, file).subscribe({
          next: () => {
            this.successMessage = `Photo uploaded for ${batch.designName || 'manufactured shirts'}!`;
            this.fetchBatches();
            this.fetchProducts();
            setTimeout(() => this.successMessage = '', 3000);
          },
          error: (err) => {
            this.errorMessage = err.error?.message || 'Failed to upload photo.';
            setTimeout(() => this.errorMessage = '', 4000);
          }
        });
      } else {
        alert('Please complete the batch to link it to a product before uploading photo, or upload from Item Master.');
      }
    }
  }

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
    fabricPerShirt: 1.30,
    setSize: 4,
    setRatio: '38, 40, 42, 44',
    sellingPrice: 325,
    distributorPrice: 300
  };

  // Complete Production Batch Form
  completionBatch = {
    batchId: 0,
    batchCode: '',
    designName: '',
    stitchingRatePerShirt: 0,
    quantityProduced: 0,
    tailoringCost: 0,
    additionalCost: 0,
    fabricCost: 0
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
    this.loadCompanyProfile();
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
        this.fetchProfitReports();
        this.fetchExpenses();
        this.fetchSalesOrders();
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
      case 'expenses':
        this.fetchExpenses();
        break;
      case 'reports':
        this.fetchProfitReports();
        break;
      case 'settings':
        this.loadCompanyProfile();
        break;
      case 'wholesale':
        this.fetchWholesaleCustomers();
        this.fetchWholesaleOrders();
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
  // SEARCHABLE PRODUCT DROPDOWN FOR POS
  // ==========================================

  get filteredProducts() {
    if (!this.itemSearchQuery) {
      return this.products;
    }
    const q = this.itemSearchQuery.toLowerCase().trim();
    return this.products.filter(p => 
      (p.name && p.name.toLowerCase().includes(q)) ||
      (p.size && p.size.toLowerCase().includes(q)) ||
      (p.color && p.color.toLowerCase().includes(q)) ||
      (p.category && p.category.toLowerCase().includes(q)) ||
      (p.productType && p.productType.toLowerCase().includes(q)) ||
      (p.designBrand && p.designBrand.toLowerCase().includes(q))
    );
  }

  selectProduct(p: any) {
    this.cartProductId = p.id;
    this.selectedProduct = p;
    this.cartUnitPrice = p.sellingPrice || 0;
    this.isItemDropdownOpen = false;
    this.itemSearchQuery = '';
  }

  clearSelectedProduct() {
    this.cartProductId = null;
    this.selectedProduct = null;
    this.cartUnitPrice = 0;
    this.itemSearchQuery = '';
  }

  toggleItemDropdown() {
    this.isItemDropdownOpen = !this.isItemDropdownOpen;
  }

  closeItemDropdown() {
    setTimeout(() => {
      this.isItemDropdownOpen = false;
    }, 200);
  }

  // ==========================================
  // OPERATING EXPENSES HELPERS & GETTERS
  // ==========================================

  get filteredExpenses() {
    if (!this.expenseSearchQuery) {
      return this.expenses;
    }
    const q = this.expenseSearchQuery.toLowerCase().trim();
    return this.expenses.filter(e =>
      (e.title && e.title.toLowerCase().includes(q)) ||
      (e.category && e.category.toLowerCase().includes(q)) ||
      (e.paymentMode && e.paymentMode.toLowerCase().includes(q)) ||
      (e.notes && e.notes.toLowerCase().includes(q))
    );
  }

  get totalExpensesAmount() {
    return this.expenses.reduce((sum, e) => sum + (e.amount || 0), 0);
  }

  get thisMonthExpensesAmount() {
    const now = new Date();
    const currentMonth = now.getMonth();
    const currentYear = now.getFullYear();
    return this.expenses.reduce((sum, e) => {
      const d = new Date(e.expenseDate);
      if (d.getMonth() === currentMonth && d.getFullYear() === currentYear) {
        return sum + (e.amount || 0);
      }
      return sum;
    }, 0);
  }

  get topExpenseCategory() {
    if (this.expenses.length === 0) return 'None';
    const categoryTotals: { [key: string]: number } = {};
    for (const exp of this.expenses) {
      const cat = exp.category || 'General';
      categoryTotals[cat] = (categoryTotals[cat] || 0) + (exp.amount || 0);
    }
    let topCat = 'None';
    let maxVal = 0;
    for (const cat in categoryTotals) {
      if (categoryTotals[cat] > maxVal) {
        maxVal = categoryTotals[cat];
        topCat = cat;
      }
    }
    return topCat;
  }

  // ==========================================
  // OVERVIEW MONTHLY DRILLDOWN AGGREGATION
  // ==========================================

  toggleDrilldown(type: 'sales' | 'profit' | 'manufacturing' | 'trading') {
    if (this.activeDrilldown === type) {
      this.activeDrilldown = 'none';
    } else {
      this.activeDrilldown = type;
      if (this.profitReports.length === 0) {
        this.fetchProfitReports();
      }
      if (this.expenses.length === 0) {
        this.fetchExpenses();
      }
      if (this.salesOrders.length === 0) {
        this.fetchSalesOrders();
      }
    }
  }

  get monthlySummaryList() {
    const map = new Map<string, {
      monthKey: string,
      dateObj: Date,
      totalSales: number,
      orderCount: number,
      mfgSales: number,
      mfgCost: number,
      mfgProfit: number,
      mfgUnits: number,
      tradingSales: number,
      tradingCost: number,
      tradingProfit: number,
      tradingUnits: number,
      grossProfit: number,
      expenses: number,
      netProfit: number
    }>();

    const getOrCreate = (d: any) => {
      const date = d ? new Date(d) : new Date();
      const monthKey = date.toLocaleDateString('en-US', { year: 'numeric', month: 'short' });
      if (!map.has(monthKey)) {
        map.set(monthKey, {
          monthKey,
          dateObj: new Date(date.getFullYear(), date.getMonth(), 1),
          totalSales: 0,
          orderCount: 0,
          mfgSales: 0,
          mfgCost: 0,
          mfgProfit: 0,
          mfgUnits: 0,
          tradingSales: 0,
          tradingCost: 0,
          tradingProfit: 0,
          tradingUnits: 0,
          grossProfit: 0,
          expenses: 0,
          netProfit: 0
        });
      }
      return map.get(monthKey)!;
    };

    // Aggregate Profit Reports
    for (const rep of this.profitReports) {
      const entry = getOrCreate(rep.salesDate);
      entry.totalSales += (rep.itemRevenue || 0);
      entry.grossProfit += (rep.netProfit || 0);

      if (rep.productType === 'MANUFACTURED') {
        entry.mfgSales += (rep.itemRevenue || 0);
        entry.mfgCost += (rep.totalCostBasis || 0);
        entry.mfgProfit += (rep.netProfit || 0);
        entry.mfgUnits += (rep.quantitySold || 0);
      } else {
        entry.tradingSales += (rep.itemRevenue || 0);
        entry.tradingCost += (rep.totalCostBasis || 0);
        entry.tradingProfit += (rep.netProfit || 0);
        entry.tradingUnits += (rep.quantitySold || 0);
      }
    }

    // Aggregate Sales Orders (for invoice counts)
    for (const order of this.salesOrders) {
      const entry = getOrCreate(order.salesDate);
      entry.orderCount += 1;
    }

    // Aggregate Expenses
    for (const exp of this.expenses) {
      const entry = getOrCreate(exp.expenseDate);
      entry.expenses += (exp.amount || 0);
    }

    // Calculate clear net profit per month
    for (const entry of map.values()) {
      entry.netProfit = entry.grossProfit - entry.expenses;
    }

    return Array.from(map.values()).sort((a, b) => b.dateObj.getTime() - a.dateObj.getTime());
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

  downloadInvoice(order: any, withGst: boolean = false) {
    if (!order) return;

    // If items array is empty or missing, fetch full order by ID
    if (!order.items || order.items.length === 0) {
      if (order.id) {
        this.apiService.getSalesOrderById(order.id).subscribe({
          next: (fullOrder) => this.generateInvoicePdf(fullOrder, withGst),
          error: () => this.generateInvoicePdf(order, withGst)
        });
        return;
      }
    }

    this.generateInvoicePdf(order, withGst);
  }

  convertNumberToWords(amount: number): string {
    const units = ['', 'One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight', 'Nine', 'Ten', 'Eleven', 'Twelve', 'Thirteen', 'Fourteen', 'Fifteen', 'Sixteen', 'Seventeen', 'Eighteen', 'Nineteen'];
    const tens = ['', '', 'Twenty', 'Thirty', 'Forty', 'Fifty', 'Sixty', 'Seventy', 'Eighty', 'Ninety'];

    function numToWords(num: number): string {
      if (num === 0) return '';
      if (num < 20) return units[num] + ' ';
      if (num < 100) return tens[Math.floor(num / 10)] + (num % 10 !== 0 ? ' ' + units[num % 10] : '') + ' ';
      if (num < 1000) return units[Math.floor(num / 100)] + ' Hundred ' + numToWords(num % 100);
      if (num < 100000) return numToWords(Math.floor(num / 1000)) + 'Thousand ' + numToWords(num % 1000);
      if (num < 10000000) return numToWords(Math.floor(num / 100000)) + 'Lakh ' + numToWords(num % 100000);
      return numToWords(Math.floor(num / 10000000)) + 'Crore ' + numToWords(num % 10000000);
    }

    const rounded = Math.round(amount);
    if (rounded === 0) return 'Zero Rupees Only';
    return 'INR ' + numToWords(rounded).trim() + ' Only';
  }

  generateInvoicePdf(order: any, withGst: boolean = false) {
    const doc = new jsPDF('p', 'mm', 'a4');
    const pageWidth = doc.internal.pageSize.getWidth(); // 210mm
    const pageHeight = doc.internal.pageSize.getHeight(); // 297mm
    const margin = 14;
    const contentWidth = pageWidth - (margin * 2); // 182mm
    const rightMarginX = pageWidth - margin; // 196mm
    const profile = this.companyProfile;

    // --- 1. TOP ACCENT BAR ---
    doc.setFillColor(37, 99, 235); // #2563eb Primary Blue Accent
    doc.rect(0, 0, pageWidth, 4, 'F');

    // --- 2. HEADER SECTION (Clean Corporate Split Header) ---
    // Left: Monogram / Logo Mark
    doc.setFillColor(15, 23, 42); // #0f172a Dark Slate
    doc.roundedRect(margin, 12, 12, 12, 2, 2, 'F');
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(10);
    doc.setTextColor(255, 255, 255);
    doc.text('CE', margin + 6, 20, { align: 'center' });

    // Company Name & Subtitle
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(16);
    doc.setTextColor(15, 23, 42);
    doc.text(profile.name, margin + 16, 18);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8);
    doc.setTextColor(100, 116, 139);
    doc.text(profile.tagline, margin + 16, 23);
    doc.text(profile.address, margin, 31);
    doc.text(`Phone: ${profile.phone}  |  GSTIN: ${profile.gstin}  |  State: ${profile.state}`, margin, 35.5);

    // Right: TAX INVOICE Header & Badge
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(18);
    doc.setTextColor(15, 23, 42);
    doc.text('TAX INVOICE', rightMarginX, 19, { align: 'right' });

    // Paid Status Badge
    doc.setFillColor(220, 252, 231); // #dcfce7
    doc.roundedRect(rightMarginX - 22, 22.5, 22, 5.5, 1.5, 1.5, 'F');
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(7.5);
    doc.setTextColor(22, 101, 52); // #166534
    doc.text('PAID', rightMarginX - 11, 26.5, { align: 'center' });

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(7.5);
    doc.setTextColor(100, 116, 139);
    doc.text('ORIGINAL FOR RECIPIENT', rightMarginX, 35.5, { align: 'right' });

    // Thin separator line
    doc.setDrawColor(226, 232, 240);
    doc.setLineWidth(0.5);
    doc.line(margin, 40, rightMarginX, 40);

    // --- 3. INVOICE META & BILL TO SECTION (Clean 2-Column Cards) ---
    const metaY = 44;
    const colW = (contentWidth - 6) / 2; // 88mm
    const cardH = 30;

    // Card 1: Billed To (Customer)
    doc.setFillColor(248, 250, 252); // #f8fafc
    doc.setDrawColor(226, 232, 240);
    doc.roundedRect(margin, metaY, colW, cardH, 2, 2, 'FD');

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(7.5);
    doc.setTextColor(100, 116, 139);
    doc.text('BILLED TO / CUSTOMER DETAILS', margin + 4, metaY + 6);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(10);
    doc.setTextColor(15, 23, 42);
    doc.text(order.customerName || 'Valued Customer', margin + 4, metaY + 13);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8);
    doc.setTextColor(71, 85, 105);
    doc.text(`Phone: ${order.customerPhone || 'N/A'}`, margin + 4, metaY + 19);
    doc.text(`Place of Supply: Maharashtra (Code 27)`, margin + 4, metaY + 24);

    // Card 2: Invoice Metadata
    const rightColX = margin + colW + 6;
    doc.setFillColor(248, 250, 252);
    doc.roundedRect(rightColX, metaY, colW, cardH, 2, 2, 'FD');

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(7.5);
    doc.setTextColor(100, 116, 139);
    doc.text('INVOICE INFORMATION', rightColX + 4, metaY + 6);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8);
    doc.setTextColor(100, 116, 139);
    doc.text('Invoice No:', rightColX + 4, metaY + 13);
    doc.text('Invoice Date:', rightColX + 4, metaY + 18.5);
    doc.text('Payment Mode:', rightColX + 4, metaY + 24);

    doc.setFont('helvetica', 'bold');
    doc.setTextColor(15, 23, 42);
    doc.text(`${order.invoiceNo}`, rightColX + 28, metaY + 13);

    const dateStr = order.salesDate ? new Date(order.salesDate).toLocaleString('en-IN', {
      day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit'
    }) : 'N/A';
    doc.setFont('helvetica', 'normal');
    doc.text(`${dateStr}`, rightColX + 28, metaY + 18.5);
    doc.text('Cash / UPI (Immediate)', rightColX + 28, metaY + 24);

    // --- 4. ITEM DETAILS TABLE ---
    const items = order.items && order.items.length > 0 ? order.items : [];
    let tableBody: any[] = [];
    let totalQty = 0;
    let totalDiscount = 0;
    let calcTotalTaxable = 0;
    let calcTotalGst = 0;

    if (items.length > 0) {
      tableBody = items.map((item: any, idx: number) => {
        const prod = item.product || this.products.find(p => p.id === item.productId) || {};
        const prodName = prod.name || item.productName || `Item #${item.productId || idx + 1}`;
        
        const tags: string[] = [];
        if (prod.category) tags.push(prod.category);
        if (prod.size) tags.push(`Size: ${prod.size}`);
        if (prod.color) tags.push(`Color: ${prod.color}`);
        if (prod.designBrand) tags.push(`Brand: ${prod.designBrand}`);

        const itemDesc = tags.length > 0 ? `${prodName}\n${tags.join('  •  ')}` : prodName;

        const qty = item.quantity || 1;
        totalQty += qty;
        const discount = item.discount || 0;
        totalDiscount += discount;

        const finalUnitPrice = item.unitPrice || 0;
        let unitRate = finalUnitPrice;
        let gstPercent = 0;
        let gstAmount = 0;
        let subtotalExclTax = (qty * finalUnitPrice) - discount;
        let lineTotal = subtotalExclTax;

        if (withGst) {
          gstPercent = item.gstPercent ?? prod.gstPercent ?? 12.00;
          unitRate = finalUnitPrice / (1 + (gstPercent / 100.0));
          subtotalExclTax = (qty * unitRate) - discount;
          gstAmount = (qty * finalUnitPrice) - discount - subtotalExclTax;
          lineTotal = subtotalExclTax + gstAmount;
        }
        
        calcTotalTaxable += subtotalExclTax;
        calcTotalGst += gstAmount;

        return [
          (idx + 1).toString().padStart(2, '0'),
          itemDesc,
          item.itemType || 'PCS',
          qty.toString(),
          `Rs. ${unitRate.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`,
          discount > 0 ? `Rs. ${discount.toFixed(2)}` : '-',
          `${gstPercent}%`,
          `Rs. ${subtotalExclTax.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`,
          `Rs. ${lineTotal.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
        ];
      });
    } else {
      const finalAmt = order.finalAmount || order.totalAmount || 0;
      let billGstPercent = 0;
      let billUnitRate = finalAmt;
      let billTaxable = finalAmt;

      if (withGst) {
        billGstPercent = order.totalGst > 0 ? ((order.totalGst / (order.totalAmount || 1)) * 100) : 12.0;
        billUnitRate = finalAmt / (1 + (billGstPercent / 100.0));
        billTaxable = billUnitRate;
      }
      
      calcTotalTaxable = billTaxable;
      calcTotalGst = finalAmt - billTaxable;

      tableBody = [
        [
          '01',
          'Garments / Apparel Items (Consolidated Bill)',
          'PCS',
          '1',
          `Rs. ${billUnitRate.toFixed(2)}`,
          '-',
          `${billGstPercent.toFixed(0)}%`,
          `Rs. ${billTaxable.toFixed(2)}`,
          `Rs. ${finalAmt.toFixed(2)}`
        ]
      ];
      totalQty = 1;
    }

    autoTable(doc, {
      startY: metaY + cardH + 5,
      margin: { left: margin, right: margin },
      head: [['#', 'ITEMS & DESCRIPTION', 'TYPE', 'QTY', 'RATE', 'DISCOUNT', 'GST', 'TAXABLE', 'TOTAL (Rs.)']],
      body: tableBody,
      theme: 'plain',
      headStyles: {
        fillColor: [241, 245, 249], // #f1f5f9 Clean subtle header
        textColor: [15, 23, 42],
        fontStyle: 'bold',
        fontSize: 8,
        halign: 'center',
        cellPadding: { top: 3.5, bottom: 3.5, left: 2, right: 2 },
        lineWidth: { bottom: 0.5 },
        lineColor: [203, 213, 225]
      },
      styles: {
        fontSize: 8,
        cellPadding: { top: 3, bottom: 3, left: 2, right: 2 },
        textColor: [30, 41, 59],
        valign: 'middle',
        lineWidth: { bottom: 0.2 },
        lineColor: [241, 245, 249]
      },
      columnStyles: {
        0: { halign: 'center', cellWidth: 8, textColor: [100, 116, 139] },
        1: { halign: 'left', cellWidth: 'auto', fontStyle: 'bold' },
        2: { halign: 'center', cellWidth: 13 },
        3: { halign: 'center', cellWidth: 11, fontStyle: 'bold' },
        4: { halign: 'right', cellWidth: 22 },
        5: { halign: 'right', cellWidth: 18, textColor: [100, 116, 139] },
        6: { halign: 'center', cellWidth: 13 },
        7: { halign: 'right', cellWidth: 23 },
        8: { halign: 'right', cellWidth: 25, fontStyle: 'bold', textColor: [15, 23, 42] }
      },
      alternateRowStyles: {
        fillColor: [255, 255, 255]
      }
    });

    const tableEndY = (doc as any).lastAutoTable.finalY || 140;

    // --- 5. SUMMARY & TOTALS SECTION ---
    const summaryStartY = tableEndY + 6;
    const summaryCardW = 90; // Expanded width to fit large currency figures smoothly
    const summaryCardX = rightMarginX - summaryCardW;
    const leftBlockW = summaryCardX - margin - 6;

    // Left Block: Amount in Words + Bank / Payment details + Terms
    // Amount in Words Card
    doc.setFillColor(248, 250, 252);
    doc.setDrawColor(226, 232, 240);
    doc.roundedRect(margin, summaryStartY, leftBlockW, 14, 2, 2, 'FD');

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(7);
    doc.setTextColor(100, 116, 139);
    doc.text('TOTAL AMOUNT IN WORDS', margin + 4, summaryStartY + 4.5);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(8);
    doc.setTextColor(15, 23, 42);
    const words = this.convertNumberToWords(order.finalAmount || 0);
    doc.text(words, margin + 4, summaryStartY + 9.5, { maxWidth: leftBlockW - 8 });

    // Bank & Payment Information Box
    const bankY = summaryStartY + 17;
    doc.setFillColor(248, 250, 252);
    doc.roundedRect(margin, bankY, leftBlockW, 20, 2, 2, 'FD');

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(7.5);
    doc.setTextColor(37, 99, 235); // Blue
    doc.text('BANK & PAYMENT DETAILS', margin + 4, bankY + 5);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(7.5);
    doc.setTextColor(71, 85, 105);
    doc.text(`Bank Name: ${profile.bankName}  |  A/C Name: ${profile.accountName}`, margin + 4, bankY + 9.5);
    doc.text(`A/C No: ${profile.accountNumber}  |  IFSC: ${profile.ifscCode}`, margin + 4, bankY + 13.5);
    doc.text(`UPI ID: ${profile.upiId}  |  GPay / PhonePe: ${profile.gpayPhone}`, margin + 4, bankY + 17.5);

    // Terms & Conditions
    const termsY = bankY + 23;
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(7.5);
    doc.setTextColor(15, 23, 42);
    doc.text('Terms & Conditions:', margin, termsY);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(7);
    doc.setTextColor(100, 116, 139);
    let termOffset = 4;
    for (const term of profile.terms) {
      doc.text(term, margin, termsY + termOffset);
      termOffset += 3.5;
    }

    // Right Block: Financial Breakdown Table
    autoTable(doc, {
      startY: summaryStartY,
      margin: { left: summaryCardX, right: margin },
      body: [
        ['Total Quantity:', `${totalQty} Units`],
        ['Taxable Net Subtotal:', `Rs. ${calcTotalTaxable.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`],
        ['Total Discount:', totalDiscount > 0 ? `- Rs. ${totalDiscount.toFixed(2)}` : 'Rs. 0.00'],
        ['CGST (Tax):', `+ Rs. ${(calcTotalGst / 2).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`],
        ['SGST (Tax):', `+ Rs. ${(calcTotalGst / 2).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`],
        ['TOTAL PAYABLE:', `Rs. ${(order.finalAmount || 0).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`]
      ],
      theme: 'plain',
      styles: {
        fontSize: 8,
        cellPadding: { top: 1.8, bottom: 1.8, left: 3, right: 3 },
        textColor: [30, 41, 59]
      },
      columnStyles: {
        0: { halign: 'left', fontStyle: 'normal', textColor: [100, 116, 139], cellWidth: 42 },
        1: { halign: 'right', fontStyle: 'bold', textColor: [15, 23, 42], cellWidth: 48 }
      },
      didParseCell: (data) => {
        if (data.row.index === 5) {
          // Highlight Grand Total row with high-end Dark Slate
          data.cell.styles.fillColor = [15, 23, 42]; // #0f172a Dark Navy
          data.cell.styles.textColor = [255, 255, 255];
          data.cell.styles.fontSize = 9.5;
          data.cell.styles.fontStyle = 'bold';
          data.cell.styles.cellPadding = { top: 3.2, bottom: 3.2, left: 3, right: 3 };
        }
      }
    });

    const summaryEndY = (doc as any).lastAutoTable.finalY || (summaryStartY + 45);
    const bottomBlockY = Math.max(summaryEndY, termsY + termOffset + 2);

    // --- 6. SIGNATURE & STAMP BLOCK ---
    const sigY = bottomBlockY + 8;
    if (sigY + 22 < pageHeight - 12) {
      doc.setDrawColor(203, 213, 225);
      doc.setLineWidth(0.4);
      doc.line(rightMarginX - 55, sigY + 14, rightMarginX, sigY + 14);

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8);
      doc.setTextColor(15, 23, 42);
      doc.text(`For ${profile.name}`, rightMarginX - 27.5, sigY + 5, { align: 'center' });

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(7.5);
      doc.setTextColor(100, 116, 139);
      doc.text('Authorized Signatory', rightMarginX - 27.5, sigY + 19, { align: 'center' });
    }

    // --- 7. CLEAN FOOTER BAR ---
    const footerY = pageHeight - 8;
    doc.setDrawColor(241, 245, 249);
    doc.setLineWidth(0.5);
    doc.line(margin, footerY - 4, rightMarginX, footerY - 4);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(7);
    doc.setTextColor(148, 163, 184);
    doc.text(`Thank you for choosing ${profile.name}! | Computer Generated Tax Invoice`, pageWidth / 2, footerY, { align: 'center' });

    doc.save(`Invoice_${order.invoiceNo}.pdf`);
  }

  fetchProfitReports() {
    this.apiService.getProfitReport().subscribe({
      next: (data) => this.profitReports = data,
      error: (err) => this.errorMessage = 'Failed to load profit reports.'
    });
  }

  fetchExpenses() {
    this.apiService.getExpenses().subscribe({
      next: (data) => this.expenses = data,
      error: (err) => this.errorMessage = 'Failed to load operating expenses.'
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
      next: (createdProd: any) => {
        if (this.selectedProductImageFile && createdProd && createdProd.id) {
          this.apiService.uploadProductImage(createdProd.id, this.selectedProductImageFile).subscribe({
            next: () => {
              this.successMessage = 'Product & Photo saved successfully!';
              this.fetchProducts();
              this.clearProductImageSelection();
            },
            error: () => {
              this.successMessage = 'Product created, but photo upload failed.';
              this.fetchProducts();
            }
          });
        } else {
          this.successMessage = 'Product created successfully!';
          this.fetchProducts();
        }

        // Reset form
        this.newProduct = {
          name: '',
          category: 'Shirts',
          productType: 'MANUFACTURED',
          designBrand: '',
          size: '38, 40, 42, 44',
          color: '',
          sellingPrice: 0,
          distributorPrice: 0,
          setSize: 4,
          setRatio: '38, 40, 42, 44',
          gstPercent: 12.00,
          imageUrl: ''
        };
        this.clearProductImageSelection();
      },
      error: (err) => this.errorMessage = err.error?.message || 'Failed to create product.'
    });
  }

  onUploadDirectPhoto(productId: number, event: any) {
    const file = event.target.files[0];
    if (file) {
      this.clearMessages();
      this.apiService.uploadProductImage(productId, file).subscribe({
        next: () => {
          this.successMessage = 'Product photo updated successfully!';
          this.fetchProducts();
          setTimeout(() => this.successMessage = '', 3000);
        },
        error: (err) => {
          this.errorMessage = err.error?.message || 'Failed to upload product photo.';
          setTimeout(() => this.errorMessage = '', 4000);
        }
      });
    }
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
        this.newBatch = { batchCode: '', fabricId: null, fabricMetersUsed: 0, wastageMeters: 0, productId: null, designName: '', quantityToSew: 0, fabricPerShirt: 1.30, setSize: 4, setRatio: '38, 40, 42, 44', sellingPrice: 325, distributorPrice: 300 };
      },
      error: (err) => this.errorMessage = err.error?.message || 'Failed to start production batch.'
    });
  }

  openCompletionModal(batch: any) {
    const qty = batch.quantityToSew || batch.quantityProduced || 0;
    const fabricCostPerMeter = batch.fabric?.costPerMeter || 0;
    const fabricCost = (batch.fabricMetersUsed || 0) * fabricCostPerMeter;

    this.completionBatch = {
      batchId: batch.id,
      batchCode: batch.batchCode || '',
      designName: batch.product?.name || batch.designName || `Batch ${batch.batchCode}`,
      stitchingRatePerShirt: 0,
      quantityProduced: qty,
      tailoringCost: 0,
      additionalCost: 0,
      fabricCost: fabricCost
    };
    this.showCompletionModal = true;
  }

  onStitchingRateOrQtyChange() {
    const rate = this.completionBatch.stitchingRatePerShirt || 0;
    const qty = this.completionBatch.quantityProduced || 0;
    this.completionBatch.tailoringCost = +(rate * qty).toFixed(2);
  }

  onTailoringCostChange() {
    const total = this.completionBatch.tailoringCost || 0;
    const qty = this.completionBatch.quantityProduced || 0;
    if (qty > 0) {
      this.completionBatch.stitchingRatePerShirt = +(total / qty).toFixed(2);
    }
  }

  get completionTotalCost() {
    return (this.completionBatch.fabricCost || 0) + (this.completionBatch.tailoringCost || 0) + (this.completionBatch.additionalCost || 0);
  }

  get completionCostPerPiece() {
    const qty = this.completionBatch.quantityProduced || 0;
    if (qty <= 0) return 0;
    return this.completionTotalCost / qty;
  }

  onSubmitCompletion() {
    this.clearMessages();
    const payload = {
      batchId: this.completionBatch.batchId,
      tailoringCost: this.completionBatch.tailoringCost,
      additionalCost: this.completionBatch.additionalCost,
      quantityProduced: this.completionBatch.quantityProduced
    };

    this.apiService.completeBatch(payload).subscribe({
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
    this.clearSelectedProduct();
    this.cartQuantity = 1;
    this.cartUnitPrice = 0;
    this.cartDiscount = 0;
  }

  recalculateCartLine(item: any) {
    const qty = item.quantity;
    const baseSub = (qty * item.unitPrice) - item.discount;
    item.subTotal = baseSub;
  }

  removeFromCart(index: number) {
    this.salesCart.splice(index, 1);
  }

  get cartPreTaxTotal() {
    return this.salesCart.reduce((sum, item) => sum + ((item.quantity * item.unitPrice) - item.discount), 0);
  }

  get cartTaxTotal() {
    return 0;
  }

  get cartFinalTotal() {
    return this.cartPreTaxTotal;
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

  // ==========================================
  // OPERATING EXPENSES ACTIONS
  // ==========================================

  onSubmitExpense() {
    this.clearMessages();
    if (!this.newExpense.title.trim()) {
      this.errorMessage = 'Please enter an expense title or description.';
      return;
    }
    if (this.newExpense.amount <= 0) {
      this.errorMessage = 'Please enter a valid expense amount greater than 0.';
      return;
    }

    const payload = {
      title: this.newExpense.title.trim(),
      category: this.newExpense.category,
      amount: Number(this.newExpense.amount),
      expenseDate: this.newExpense.expenseDate ? new Date(this.newExpense.expenseDate).toISOString() : new Date().toISOString(),
      paymentMode: this.newExpense.paymentMode,
      notes: this.newExpense.notes ? this.newExpense.notes.trim() : null
    };

    this.apiService.createExpense(payload).subscribe({
      next: () => {
        this.successMessage = 'Expense voucher recorded successfully!';
        this.newExpense = {
          title: '',
          category: 'Rent',
          amount: 0,
          expenseDate: new Date().toISOString().substring(0, 10),
          paymentMode: 'CASH',
          notes: ''
        };
        this.fetchExpenses();
        this.fetchDashboardSummary();
      },
      error: (err) => this.errorMessage = err.error?.message || 'Failed to record expense.'
    });
  }

  onDeleteExpense(id: number) {
    if (confirm('Are you sure you want to delete this expense record?')) {
      this.clearMessages();
      this.apiService.deleteExpense(id).subscribe({
        next: () => {
          this.successMessage = 'Expense record deleted successfully.';
          this.fetchExpenses();
          this.fetchDashboardSummary();
        },
        error: (err) => this.errorMessage = err.error?.message || 'Failed to delete expense.'
      });
    }
  }

  // ==========================================
  // WHOLESALE BUYERS & ORDERS (ADMIN)
  // ==========================================

  fetchWholesaleCustomers() {
    this.apiService.getWholesaleCustomers().subscribe({
      next: (data) => this.wholesaleCustomers = data,
      error: (err) => console.error('Failed to load wholesale customers:', err)
    });
  }

  fetchWholesaleOrders() {
    this.apiService.getPortalWholesaleOrders().subscribe({
      next: (data) => this.wholesaleOrders = data,
      error: (err) => console.error('Failed to load wholesale orders:', err)
    });
  }

  approveCustomer(customer: any, type: string) {
    this.clearMessages();
    this.apiService.updateCustomerType(customer.id, type).subscribe({
      next: () => {
        this.apiService.updateCustomerStatus(customer.id, 'ACTIVE').subscribe({
          next: () => {
            this.successMessage = `Approved ${customer.shopName} as ${type} customer successfully!`;
            this.fetchWholesaleCustomers();
          },
          error: (err) => this.errorMessage = err.error?.message || 'Failed to activate customer.'
        });
      },
      error: (err) => this.errorMessage = err.error?.message || 'Failed to set pricing group.'
    });
  }

  setCustomerStatus(customer: any, status: string) {
    this.clearMessages();
    this.apiService.updateCustomerStatus(customer.id, status).subscribe({
      next: () => {
        this.successMessage = `Customer ${customer.shopName} marked as ${status}.`;
        this.fetchWholesaleCustomers();
      },
      error: (err) => this.errorMessage = err.error?.message || 'Failed to update status.'
    });
  }

  setCustomerType(customer: any, type: string) {
    this.clearMessages();
    this.apiService.updateCustomerType(customer.id, type).subscribe({
      next: () => {
        this.successMessage = `Pricing tier for ${customer.shopName} updated to ${type}.`;
        this.fetchWholesaleCustomers();
      },
      error: (err) => this.errorMessage = err.error?.message || 'Failed to update pricing group.'
    });
  }

  changeOrderStatus(order: any, newStatus: string) {
    this.clearMessages();
    this.apiService.updateOrderStatus(order.id, newStatus).subscribe({
      next: () => {
        this.successMessage = `Order #${order.invoiceNo} status updated to ${newStatus}.`;
        this.fetchWholesaleOrders();
      },
      error: (err) => this.errorMessage = err.error?.message || 'Failed to update order status.'
    });
  }

  filteredWholesaleCustomers() {
    if (!this.customerSearchQuery.trim()) return this.wholesaleCustomers;
    const q = this.customerSearchQuery.toLowerCase();
    return this.wholesaleCustomers.filter(c => 
      (c.shopName && c.shopName.toLowerCase().includes(q)) || 
      (c.ownerName && c.ownerName.toLowerCase().includes(q)) || 
      (c.phone && c.phone.toLowerCase().includes(q)) || 
      (c.city && c.city.toLowerCase().includes(q))
    );
  }

  filteredWholesaleOrders() {
    if (this.orderStatusFilter === 'ALL') return this.wholesaleOrders;
    return this.wholesaleOrders.filter(o => o.orderStatus === this.orderStatusFilter);
  }

  countPendingCustomers(): number {
    return this.wholesaleCustomers.filter(c => c.status === 'PENDING').length;
  }
}
