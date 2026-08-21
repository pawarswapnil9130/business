/**
 * CASA ENTERPRISES — E-COMMERCE WHOLESALE B2B PORTAL
 * Myntra / Flipkart / Zepto Style Application Logic & State Controller
 */

// API Base URL configuration
const API_BASE = (window.location.port === '5000') 
  ? '/api' 
  : ((window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1' || window.location.hostname.startsWith('192.168.'))
      ? 'http://localhost:5000/api' 
      : 'https://business-backend-q18v.onrender.com/api');

const BACKEND_ORIGIN = (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1')
  ? (window.location.port === '5000' ? '' : 'http://localhost:5000')
  : 'https://business-backend-q18v.onrender.com';

function resolveImageUrl(url) {
  if (!url) return '';
  if (url.startsWith('http://') || url.startsWith('https://') || url.startsWith('data:')) {
    return url;
  }
  return BACKEND_ORIGIN + (url.startsWith('/') ? url : '/' + url);
}

// Application State
const state = {
  token: localStorage.getItem('casa_customer_token') || null,
  customer: JSON.parse(localStorage.getItem('casa_customer_profile') || 'null'),
  products: [],
  filteredProducts: [],
  cart: {}, // { [productId]: { product, quantity } }
  activeCategory: 'ALL',
  quickFilter: 'ALL',
  searchQuery: '',
  tempSessionToken: null
};

// =======================================================
// INITIALIZATION
// =======================================================

document.addEventListener('DOMContentLoaded', () => {
  renderAuthState();
  loadCatalog();

  // Close dropdowns on outside click
  document.addEventListener('click', (e) => {
    const accountWrapper = document.querySelector('.account-menu-wrapper');
    const dropdown = document.getElementById('account-dropdown');
    if (accountWrapper && !accountWrapper.contains(e.target) && dropdown) {
      dropdown.classList.remove('open');
    }
  });
});

// Update Header, Welcome bar, and UI based on Authentication state
function renderAuthState() {
  const isLoggedIn = !!state.token && !!state.customer;

  const headerUnauth = document.getElementById('header-unauth-group');
  const headerAuth = document.getElementById('header-auth-group');
  const headerShopName = document.getElementById('header-shop-name');
  const headerTierTag = document.getElementById('header-tier-tag');

  const b2bUnauthBanner = document.getElementById('b2b-unauth-banner');
  const b2bAuthBanner = document.getElementById('b2b-auth-banner');
  const authBannerShop = document.getElementById('auth-banner-shop');
  const authBannerOwner = document.getElementById('auth-banner-owner');
  const authBannerCity = document.getElementById('auth-banner-city');
  const authBannerTier = document.getElementById('auth-banner-tier');

  const dropdownFullShop = document.getElementById('dropdown-full-shop');
  const dropdownOwnerPhone = document.getElementById('dropdown-owner-phone');
  const dropdownTierName = document.getElementById('dropdown-tier-name');
  const mobileNavAccountLabel = document.getElementById('mobile-nav-account-label');

  if (isLoggedIn) {
    // Show Authenticated Controls
    if (headerUnauth) headerUnauth.style.display = 'none';
    if (headerAuth) headerAuth.style.display = 'flex';

    if (headerShopName) headerShopName.textContent = state.customer.shopName;
    if (headerTierTag) headerTierTag.textContent = state.customer.customerType;

    if (dropdownFullShop) dropdownFullShop.textContent = state.customer.shopName;
    if (dropdownOwnerPhone) dropdownOwnerPhone.textContent = `Prop: ${state.customer.ownerName}`;
    if (dropdownTierName) dropdownTierName.textContent = state.customer.customerType === 'DISTRIBUTOR' ? '⭐ DISTRIBUTOR TIER' : '🏷️ REGULAR WHOLESALE';

    if (b2bUnauthBanner) b2bUnauthBanner.style.display = 'none';
    if (b2bAuthBanner) b2bAuthBanner.style.display = 'block';

    if (authBannerShop) authBannerShop.textContent = state.customer.shopName;
    if (authBannerOwner) authBannerOwner.textContent = state.customer.ownerName;
    if (authBannerCity) authBannerCity.textContent = state.customer.city;
    if (authBannerTier) authBannerTier.textContent = state.customer.customerType === 'DISTRIBUTOR' ? '⭐ DISTRIBUTOR RATE' : '🏷️ REGULAR WHOLESALE';

    if (mobileNavAccountLabel) mobileNavAccountLabel.textContent = state.customer.shopName.split(' ')[0];
  } else {
    // Show Public / Unauthenticated Controls
    if (headerUnauth) headerUnauth.style.display = 'flex';
    if (headerAuth) headerAuth.style.display = 'none';

    if (b2bUnauthBanner) b2bUnauthBanner.style.display = 'block';
    if (b2bAuthBanner) b2bAuthBanner.style.display = 'none';

    if (mobileNavAccountLabel) mobileNavAccountLabel.textContent = 'Login';
  }

  updateCartBadges();
}

// =======================================================
// CATALOG LOADER & RENDERING (MYNTRA & ZEPTO CARDS)
// =======================================================

async function loadCatalog() {
  const grid = document.getElementById('ecom-products-grid');
  grid.innerHTML = `
    <div class="loading-state">
      <div class="pulsing-spinner"></div>
      <p>Fetching real-time wholesale catalog from factory...</p>
    </div>
  `;

  try {
    const isLoggedIn = !!state.token;
    const url = isLoggedIn ? `${API_BASE}/customer/products` : `${API_BASE}/customer/public-showcase`;
    const headers = isLoggedIn 
      ? { 'Authorization': `Bearer ${state.token}`, 'Cache-Control': 'no-cache', 'Pragma': 'no-cache' } 
      : { 'Cache-Control': 'no-cache', 'Pragma': 'no-cache' };

    const res = await fetch(url, { headers });
    if (!res.ok) {
      if (res.status === 401) {
        handleLogout();
        return;
      }
      throw new Error('Failed to load catalog');
    }

    state.products = await res.json();
    applyFilters();
  } catch (err) {
    grid.innerHTML = `
      <div class="loading-state">
        <p style="color: #ef4444; font-weight: 700;">⚠️ Could not connect to wholesale catalog API.</p>
        <button class="btn btn-sm btn-nav-apply mt-2" onclick="loadCatalog()">🔄 Retry</button>
      </div>
    `;
  }
}

function applyFilters() {
  let list = state.products;

  // Category filter
  if (state.activeCategory !== 'ALL') {
    const cat = state.activeCategory.toLowerCase();
    list = list.filter(p => p.category && p.category.toLowerCase().includes(cat));
  }

  // Quick filter chips
  if (state.quickFilter === 'IN_STOCK') {
    list = list.filter(p => p.available);
  } else if (state.quickFilter === 'SHIRTS') {
    list = list.filter(p => p.category && p.category.toLowerCase().includes('shirt'));
  } else if (state.quickFilter === 'JEANS') {
    list = list.filter(p => p.category && p.category.toLowerCase().includes('jean'));
  }

  // Search query filter
  if (state.searchQuery.trim()) {
    const q = state.searchQuery.toLowerCase();
    list = list.filter(p => 
      (p.name && p.name.toLowerCase().includes(q)) ||
      (p.color && p.color.toLowerCase().includes(q)) ||
      (p.size && p.size.toLowerCase().includes(q)) ||
      (p.designBrand && p.designBrand.toLowerCase().includes(q)) ||
      (p.category && p.category.toLowerCase().includes(q))
    );
  }

  state.filteredProducts = list;
  renderProductsGrid();
}

function renderProductsGrid() {
  const grid = document.getElementById('ecom-products-grid');
  const countText = document.getElementById('catalog-count-text');
  const isLoggedIn = !!state.token;

  if (countText) {
    countText.textContent = `Showing ${state.filteredProducts.length} verified products available for bulk set ordering`;
  }

  if (state.filteredProducts.length === 0) {
    grid.innerHTML = `
      <div class="loading-state">
        <p style="color: var(--text-muted);">No shirts or products found matching your current filter.</p>
        <button class="btn btn-sm btn-nav-apply mt-2" onclick="resetFilters()">Reset Filters</button>
      </div>
    `;
    return;
  }

  grid.innerHTML = state.filteredProducts.map(p => {
    const setSize = p.setSize > 0 ? p.setSize : 4;
    const setRatio = p.setRatio || p.size || '38, 40, 42, 44';
    const cartItem = state.cart[p.id];
    const setsCount = cartItem?.setsCount || 0;
    const cartQty = setsCount * setSize;

    // Available full sets
    const availableStock = p.availableStock !== undefined ? p.availableStock : (p.availableSets ? p.availableSets * setSize : 0);
    const maxSets = p.availableSets !== undefined ? p.availableSets : Math.floor(availableStock / setSize);
    const isAvailable = maxSets > 0;

    // Realistic MRP for wholesale discount representation
    const unitRate = p.price ? Number(p.price) : 325;
    const setPrice = unitRate * setSize;
    const mrp = Math.round(unitRate * 2.4);
    const discountPercent = Math.round(((mrp - unitRate) / mrp) * 100);

    const stockBadge = isAvailable
      ? `<span class="stock-pill stock-in">⚡ ${maxSets} ${maxSets === 1 ? 'Set' : 'Sets'} In Stock (${maxSets * setSize} pcs)</span>`
      : '<span class="stock-pill stock-out">Out of Stock</span>';

    let priceSectionHtml = '';
    let actionSectionHtml = '';

    if (isLoggedIn) {
      // Authenticated Wholesale Buyer View
      priceSectionHtml = `
        <div class="price-unlocked-view">
          <span class="wholesale-rate-big">₹${unitRate.toFixed(2)}<small style="font-size: 11px; font-weight: normal; color: var(--text-muted);">/pc</small></span>
          <span class="set-price-tag">₹${Math.round(setPrice).toLocaleString('en-IN')} / Set (${setSize} pcs)</span>
        </div>
      `;

      if (isAvailable) {
        const isMaxReached = setsCount >= maxSets;
        if (setsCount > 0) {
          // Zepto Active Set Stepper
          actionSectionHtml = `
            <div class="card-action-wrap">
              <div class="zepto-stepper-active">
                <button class="zepto-step-btn" onclick="adjustSets(${p.id}, -1)">-</button>
                <span class="zepto-step-val">${setsCount} ${setsCount === 1 ? 'Set' : 'Sets'} (${cartQty} pcs)</span>
                <button class="zepto-step-btn" onclick="adjustSets(${p.id}, 1)" ${isMaxReached ? 'disabled title="Maximum stock reached" style="opacity:0.3; cursor:not-allowed;"' : ''}>+</button>
              </div>
              <div class="quick-multiplier-bar">
                <button class="multiplier-btn" onclick="adjustSets(${p.id}, 1)" ${setsCount + 1 > maxSets ? 'disabled style="opacity:0.4;"' : ''}>+1 Set</button>
                <button class="multiplier-btn" onclick="adjustSets(${p.id}, 2)" ${setsCount + 2 > maxSets ? 'disabled style="opacity:0.4;"' : ''}>+2 Sets</button>
                <button class="multiplier-btn" onclick="adjustSets(${p.id}, 5)" ${setsCount + 5 > maxSets ? 'disabled style="opacity:0.4;"' : ''}>+5 Sets</button>
                <button class="multiplier-btn" onclick="adjustSets(${p.id}, 10)" ${setsCount + 10 > maxSets ? 'disabled style="opacity:0.4;"' : ''}>+10 Sets</button>
              </div>
            </div>
          `;
        } else {
          // Initial ADD SET Button
          actionSectionHtml = `
            <div class="card-action-wrap">
              <button class="btn-zepto-add" onclick="adjustSets(${p.id}, 1)">
                <span>ADD SET</span>
                <span style="font-size: 11px;">1 Set (${setSize} Pcs)</span>
              </button>
              <div class="quick-multiplier-bar">
                <button class="multiplier-btn" onclick="adjustSets(${p.id}, 1)">+1 Set</button>
                <button class="multiplier-btn" onclick="adjustSets(${p.id}, 2)" ${2 > maxSets ? 'disabled style="opacity:0.4;"' : ''}>+2 Sets</button>
                <button class="multiplier-btn" onclick="adjustSets(${p.id}, 5)" ${5 > maxSets ? 'disabled style="opacity:0.4;"' : ''}>+5 Sets</button>
                <button class="multiplier-btn" onclick="adjustSets(${p.id}, 10)" ${10 > maxSets ? 'disabled style="opacity:0.4;"' : ''}>+10 Sets</button>
              </div>
            </div>
          `;
        }
      } else {
        actionSectionHtml = `
          <button class="btn-zepto-add" disabled style="opacity: 0.5; border-color: #cbd5e1; color: #94a3b8; background: #f1f5f9;">
            Out of Stock
          </button>
        `;
      }
    } else {
      // Public Visitor View: Wholesale Price Locked strictly
      priceSectionHtml = `
        <div class="price-locked-view">
          <span>🔒 Wholesale Price Locked</span>
        </div>
      `;

      actionSectionHtml = `
        <button class="btn-zepto-add" onclick="openLoginModal()">
          🔑 Login to View Rate
        </button>
      `;
    }

    const resolvedSrc = resolveImageUrl(p.imageUrl);
    const imageHtml = resolvedSrc
      ? `<img src="${resolvedSrc}" alt="${p.name}" class="ecom-card-img" onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';"><div class="card-no-img" style="display:none;"><span class="card-icon">👔</span><span class="no-img-brand">${p.designBrand || 'CASA APPAREL'}</span></div>`
      : `<div class="card-no-img"><span class="card-icon">👔</span><span class="no-img-brand">${p.designBrand || 'CASA APPAREL'}</span></div>`;

    return `
      <div class="ecom-card">
        <div class="card-media-wrapper">
          <div class="badge-top-left">
            <span class="discount-pill">${discountPercent}% OFF B2B</span>
          </div>
          <div class="badge-top-right">
            ${stockBadge}
          </div>
          ${imageHtml}
        </div>

        <div class="card-content">
          <div class="card-brand-line">
            <span class="card-brand-tag">${p.designBrand || 'CASA APPAREL'}</span>
            <span class="card-cat-tag">${p.category || 'Shirts'}</span>
          </div>
          
          <h3 class="card-item-title">${p.name}</h3>

          <div class="set-size-pill">
            <span>📦 Set of ${setSize} Pcs (${setRatio})</span>
          </div>

          <div class="card-specs-row">
            <span class="spec-chip">Color: <strong>${p.color}</strong></span>
            <span class="spec-chip">Sizes: <strong>${setRatio}</strong></span>
          </div>

          <div class="card-price-container">
            ${priceSectionHtml}
            ${actionSectionHtml}
          </div>
        </div>
      </div>
    `;
  }).join('');
}

// =======================================================
// SEARCH & FILTER HANDLERS
// =======================================================

function handleSearchFilter(val) {
  state.searchQuery = val;
  const clearBtn = document.getElementById('btn-clear-search');
  if (clearBtn) clearBtn.style.display = val ? 'block' : 'none';
  applyFilters();
}

function clearSearch() {
  const input = document.getElementById('header-search-input');
  if (input) input.value = '';
  handleSearchFilter('');
}

function setCategory(cat, element) {
  state.activeCategory = cat;
  document.querySelectorAll('.category-card').forEach(c => c.classList.remove('active'));
  if (element) element.classList.add('active');
  applyFilters();
  scrollToProducts();
}

function setQuickFilter(filter, element) {
  state.quickFilter = filter;
  document.querySelectorAll('.filter-chips .chip').forEach(c => c.classList.remove('active'));
  if (element) element.classList.add('active');
  applyFilters();
}

function resetFilters() {
  state.activeCategory = 'ALL';
  state.quickFilter = 'ALL';
  state.searchQuery = '';
  document.getElementById('header-search-input').value = '';
  document.querySelectorAll('.category-card').forEach((c, idx) => c.classList.toggle('active', idx === 0));
  document.querySelectorAll('.filter-chips .chip').forEach((c, idx) => c.classList.toggle('active', idx === 0));
  applyFilters();
}

function scrollToProducts() {
  document.getElementById('products-section')?.scrollIntoView({ behavior: 'smooth' });
}

function scrollToTop() {
  window.scrollTo({ top: 0, behavior: 'smooth' });
}

// =======================================================
// ZEPTO STYLE CART & QUANTITY STEPPER CONTROLLER (SET-TO-SET)
// =======================================================

function adjustSets(productId, deltaSets) {
  if (!state.token) {
    openLoginModal();
    return;
  }

  const product = state.products.find(p => p.id === productId);
  if (!product) return;

  const setSize = product.setSize > 0 ? product.setSize : 4;
  const availableStock = product.availableStock !== undefined ? product.availableStock : (product.availableSets ? product.availableSets * setSize : 0);
  const maxSets = product.availableSets !== undefined ? product.availableSets : Math.floor(availableStock / setSize);

  const currentSets = state.cart[productId]?.setsCount || 0;
  let newSets = currentSets + deltaSets;

  if (deltaSets > 0 && currentSets >= maxSets) {
    showToast(`⚠️ Max available: ${maxSets} ${maxSets === 1 ? 'Set' : 'Sets'} (${maxSets * setSize} pcs) in stock`);
    return;
  }

  if (newSets > maxSets) {
    newSets = maxSets;
    showToast(`⚠️ Adjusted to maximum available stock: ${maxSets} Sets (${maxSets * setSize} pcs)`);
  }

  newSets = Math.max(0, newSets);

  if (newSets > 0) {
    const totalPcs = newSets * setSize;
    state.cart[productId] = { 
      product, 
      setsCount: newSets, 
      setSize: setSize, 
      quantity: totalPcs 
    };
    showToast(`✓ Cart: ${newSets} ${newSets === 1 ? 'Set' : 'Sets'} (${totalPcs} pcs)`);
  } else {
    delete state.cart[productId];
    showToast(`Removed from wholesale cart`);
  }

  renderProductsGrid();
  updateCartBadges();
  renderCartDrawer();
}

// Backward compatibility alias
function adjustQty(productId, delta) {
  const product = state.products.find(p => p.id === productId);
  const setSize = product?.setSize > 0 ? product.setSize : 4;
  const deltaSets = Math.round(delta / setSize) || (delta > 0 ? 1 : -1);
  adjustSets(productId, deltaSets);
}

function updateCartBadges() {
  const totalSets = Object.values(state.cart).reduce((sum, item) => sum + (item.setsCount || 1), 0);
  const totalPcs = Object.values(state.cart).reduce((sum, item) => sum + item.quantity, 0);
  const totalVal = Object.values(state.cart).reduce((sum, item) => sum + (item.quantity * Number(item.product.price || 325)), 0);

  const headerCount = document.getElementById('cart-badge-count');
  const headerTotal = document.getElementById('cart-preview-total');
  const authBannerCount = document.getElementById('auth-banner-cart-count');
  const mobileBottomCount = document.getElementById('mobile-bottom-cart-count');

  if (headerCount) headerCount.textContent = totalSets;
  if (headerTotal) headerTotal.textContent = `₹${Math.round(totalVal).toLocaleString('en-IN')}`;
  if (authBannerCount) authBannerCount.textContent = `${totalSets} Sets (${totalPcs} pcs)`;
  if (mobileBottomCount) mobileBottomCount.textContent = totalSets;
}

function showToast(msg) {
  const toast = document.getElementById('toast-msg');
  if (!toast) return;
  toast.textContent = msg;
  toast.classList.add('show');
  setTimeout(() => toast.classList.remove('show'), 2000);
}

// =======================================================
// SLIDE-OUT CART DRAWER (ZEPTO STYLE)
// =======================================================

function openCartDrawer() {
  if (!state.token) {
    openLoginModal();
    return;
  }
  renderCartDrawer();
  document.getElementById('cart-drawer-overlay').classList.add('open');
  document.getElementById('cart-slide-drawer').classList.add('open');
}

function closeCartDrawer() {
  document.getElementById('cart-drawer-overlay').classList.remove('open');
  document.getElementById('cart-slide-drawer').classList.remove('open');
}

function renderCartDrawer() {
  const body = document.getElementById('drawer-cart-body');
  const items = Object.values(state.cart);
  const totalItemsBadge = document.getElementById('drawer-total-items-badge');

  if (totalItemsBadge) {
    const totalSets = items.reduce((sum, i) => sum + (i.setsCount || 1), 0);
    totalItemsBadge.textContent = `${totalSets} Sets (${items.length} designs)`;
  }

  if (items.length === 0) {
    body.innerHTML = `
      <div style="text-align: center; padding: 60px 20px; color: var(--text-muted);">
        <span style="font-size: 48px; display: block; margin-bottom: 12px;">🛒</span>
        <strong style="color: var(--text-main); font-size: 15px; display: block;">Your wholesale cart is empty</strong>
        <p style="font-size: 12px; margin-top: 4px;">Select wholesale sets to build your parcel order.</p>
        <button class="btn btn-sm btn-nav-login mt-4" onclick="closeCartDrawer(); scrollToProducts();">Explore Shirts</button>
      </div>
    `;
    document.getElementById('bill-total-pcs').textContent = '0 Sets (0 Pieces)';
    document.getElementById('bill-grand-total').textContent = '₹0.00';
    document.getElementById('btn-checkout-price-preview').textContent = '₹0.00';
    document.getElementById('btn-submit-order').disabled = true;
    return;
  }

  document.getElementById('btn-submit-order').disabled = false;

  let totalSets = 0;
  let totalQty = 0;
  let totalTaxable = 0;
  let totalGst = 0;

  body.innerHTML = items.map(({ product, setsCount, setSize, quantity }) => {
    const unitPrice = Number(product.price || 325);
    const subtotal = quantity * unitPrice;
    const gst = 0;
    const lineTotal = subtotal;
    const currentSets = setsCount || 1;
    const currentSetSize = setSize || product.setSize || 4;
    const ratio = product.setRatio || product.size || '38, 40, 42, 44';

    totalSets += currentSets;
    totalQty += quantity;
    totalTaxable += subtotal;
    totalGst += gst;

    return `
      <div class="drawer-item-card">
        <div class="drawer-item-icon">👔</div>
        <div class="drawer-item-details">
          <strong>${product.name}</strong>
          <div class="drawer-item-meta" style="color: #059669; font-weight: 700;">
            📦 ${currentSets} ${currentSets === 1 ? 'Set' : 'Sets'} × ${currentSetSize} Pcs = ${quantity} Pieces
          </div>
          <div style="font-size: 11px; color: var(--text-muted); margin-top: 2px;">
            Ratio: ${ratio} | Color: ${product.color}
          </div>
          <div class="drawer-item-pricing" style="margin-top: 8px;">
            <span class="drawer-item-price">₹${Math.round(lineTotal).toLocaleString('en-IN')} <small style="font-size: 11px; color: var(--text-muted); font-weight: normal;">(₹${(unitPrice * currentSetSize).toFixed(0)}/set)</small></span>
            <div class="zepto-stepper-active" style="padding: 2px 4px; width: 95px;">
              <button class="zepto-step-btn" style="width: 22px; height: 22px; font-size: 14px;" onclick="adjustSets(${product.id}, -1)">-</button>
              <span class="zepto-step-val" style="font-size: 12px;">${currentSets} Set${currentSets > 1 ? 's' : ''}</span>
              <button class="zepto-step-btn" style="width: 22px; height: 22px; font-size: 14px;" onclick="adjustSets(${product.id}, 1)">+</button>
            </div>
          </div>
        </div>
      </div>
    `;
  }).join('');
  const grandTotal = totalTaxable + totalGst;

  document.getElementById('bill-total-pcs').textContent = `${totalSets} Sets (${totalQty} Pieces)`;
  document.getElementById('bill-grand-total').textContent = `₹${grandTotal.toFixed(2)}`;
  document.getElementById('btn-checkout-price-preview').textContent = `₹${grandTotal.toFixed(2)}`;
  document.getElementById('btn-submit-order').disabled = false;
}

let companyProfileData = null;

async function proceedToPayment() {
  const items = Object.values(state.cart);
  if (items.length === 0) return;

  // Show loading
  const btn = document.getElementById('btn-submit-order');
  btn.disabled = true;
  btn.querySelector('.btn-checkout-text').textContent = 'LOADING PAYMENT DETAILS...';

  try {
    const res = await fetch(`${API_BASE}/settings/company_profile`);
    if (res.ok) {
      companyProfileData = await res.json();
    }
  } catch (e) {
    console.error('Failed to fetch settings', e);
  }

  btn.disabled = false;
  btn.querySelector('.btn-checkout-text').textContent = 'PROCEED TO PAYMENT';

  // Calculate Grand Total
  let grandTotal = 0;
  items.forEach(({ product, quantity }) => {
    const unitPrice = Number(product.price || 325);
    grandTotal += quantity * unitPrice;
  });

  // Hide cart, show payment
  document.getElementById('drawer-cart-body').style.display = 'none';
  document.getElementById('drawer-footer-checkout').style.display = 'none';
  document.getElementById('drawer-payment-body').style.display = 'block';

  // Fill data
  document.getElementById('payment-amount-display').textContent = `₹${grandTotal.toFixed(2)}`;
  
  if (companyProfileData) {
    document.getElementById('payment-bank-name').textContent = companyProfileData.bankName || '--';
    document.getElementById('payment-account-name').textContent = companyProfileData.accountName || '--';
    document.getElementById('payment-account-number').textContent = companyProfileData.accountNumber || '--';
    document.getElementById('payment-ifsc').textContent = companyProfileData.ifscCode || '--';
    document.getElementById('payment-upi').textContent = companyProfileData.upiId || '--';

    if (companyProfileData.qrCodeUrl) {
      const baseUrl = API_BASE.replace('/api', '');
      const qrUrl = companyProfileData.qrCodeUrl.startsWith('http') ? companyProfileData.qrCodeUrl : (baseUrl + companyProfileData.qrCodeUrl);
      document.getElementById('payment-qr-image').src = qrUrl;
      document.getElementById('payment-qr-image').style.display = 'inline-block';
      document.getElementById('payment-qr-placeholder').style.display = 'none';
    } else {
      document.getElementById('payment-qr-image').style.display = 'none';
      document.getElementById('payment-qr-placeholder').style.display = 'block';
      document.getElementById('payment-qr-placeholder').textContent = 'No QR Code provided by store.';
    }
  }
}

function backToCart() {
  document.getElementById('drawer-payment-body').style.display = 'none';
  document.getElementById('drawer-cart-body').style.display = 'block';
  document.getElementById('drawer-footer-checkout').style.display = 'block';
}

async function confirmPaymentAndPlaceOrder() {
  const transactionIdInput = document.getElementById('payment-transaction-id').value.trim();
  const proofFileInput = document.getElementById('payment-proof-file').files[0];

  if (!transactionIdInput || !proofFileInput) {
    alert("Please enter Transaction ID and upload the Payment Proof screenshot.");
    return;
  }

  const btn = document.getElementById('btn-confirm-payment');
  const errorDiv = document.getElementById('drawer-order-error');
  const notes = document.getElementById('drawer-order-notes').value.trim();

  errorDiv.style.display = 'none';

  const cartItems = Object.values(state.cart);
  const items = cartItems.map(({ product, setsCount, quantity }) => ({
    productId: product.id,
    quantity,
    setsCount: setsCount || 1
  }));

  const totalSets = cartItems.reduce((s, i) => s + (i.setsCount || 1), 0);
  const totalPcs = cartItems.reduce((s, i) => s + i.quantity, 0);

  btn.disabled = true;
  btn.textContent = 'UPLOADING PROOF...';

  try {
    // 1. Upload Payment Proof File
    let paymentProofUrl = null;
    const formData = new FormData();
    formData.append('file', proofFileInput);
    
    const uploadRes = await fetch(`${API_BASE}/customer/upload-proof`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${state.token}` },
      body: formData
    });

    if (!uploadRes.ok) throw new Error("Failed to upload payment proof.");
    const uploadData = await uploadRes.json();
    paymentProofUrl = uploadData.url;

    btn.textContent = 'PLACING ORDER...';

    // 2. Place Order
    const payload = {
      items,
      notes,
      totalSets,
      totalPcs,
      transactionId: transactionIdInput,
      paymentProofUrl: paymentProofUrl
    };

    const res = await fetch(`${API_BASE}/customer/wholesale-orders`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${state.token}`
      },
      body: JSON.stringify(payload)
    });

    if (res.ok) {
      const data = await res.json();
      
      // Clear cart
      state.cart = {};
      updateCartBadges();
      closeCartDrawer();
      showToast(`Order Placed Successfully! (Invoice #${data.invoiceNo})`);
      
      // Refresh past orders view if user navigates there
      setTimeout(() => {
        // Assuming there might be a function to refresh orders
        if (typeof fetchCustomerOrders === 'function') fetchCustomerOrders();
      }, 1500);
      
    } else {
      const err = await res.json();
      throw new Error(err.message || 'Failed to place order');
    }

  } catch (error) {
    console.error('Order placement error:', error);
    alert(error.message);
  } finally {
    btn.disabled = false;
    btn.textContent = 'CONFIRM & PLACE ORDER';
  }
}

async function submitWholesaleOrder() {
  const errorDiv = document.getElementById('drawer-order-error');
  const btn = document.getElementById('btn-submit-order');
  const notes = document.getElementById('drawer-order-notes').value.trim();

  errorDiv.style.display = 'none';

  const cartItems = Object.values(state.cart);
  const items = cartItems.map(({ product, setsCount, quantity }) => ({
    productId: product.id,
    quantity,
    setsCount: setsCount || 1
  }));

  if (items.length === 0) return;

  const totalSets = cartItems.reduce((s, i) => s + (i.setsCount || 1), 0);
  const totalPcs = cartItems.reduce((s, i) => s + i.quantity, 0);

  btn.disabled = true;
  btn.querySelector('.btn-checkout-text').textContent = 'RECORDING IN CASA ERP...';

  try {
    const res = await fetch(`${API_BASE}/customer/orders`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${state.token}`
      },
      body: JSON.stringify({ notes, items })
    });

    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Failed to place order.');

    // Clear Cart
    state.cart = {};
    updateCartBadges();
    closeCartDrawer();

    // Show Success Modal
    document.getElementById('success-order-invoice-no').textContent = `Invoice #${data.invoiceNo}`;
    document.getElementById('success-total-qty').textContent = `${totalSets} Sets (${totalPcs} Pieces)`;
    document.getElementById('success-total-amt').textContent = `₹${Number(data.finalAmount).toFixed(2)}`;
    document.getElementById('modal-order-success').style.display = 'flex';
  } catch (err) {
    errorDiv.textContent = err.message;
    errorDiv.style.display = 'block';
  } finally {
    btn.disabled = false;
    btn.querySelector('.btn-checkout-text').textContent = 'PLACE WHOLESALE ORDER';
  }
}

// =======================================================
// MODAL CONTROLLERS & 2-STEP OTP AUTHENTICATION
// =======================================================

function toggleAccountDropdown() {
  document.getElementById('account-dropdown')?.classList.toggle('open');
}

function handleAccountButtonClick() {
  if (state.token) {
    openOrdersModal();
  } else {
    openLoginModal();
  }
}

function openLoginModal() {
  document.getElementById('modal-login').style.display = 'flex';
  document.getElementById('login-step-1').style.display = 'block';
  document.getElementById('login-step-2').style.display = 'none';
  document.getElementById('login-step1-error').style.display = 'none';
  document.getElementById('login-step2-error').style.display = 'none';
}

function closeLoginModal() {
  document.getElementById('modal-login').style.display = 'none';
}

function backToLoginStep1() {
  document.getElementById('login-step-1').style.display = 'block';
  document.getElementById('login-step-2').style.display = 'none';
}

function openApplyModal() {
  document.getElementById('modal-apply').style.display = 'flex';
  document.getElementById('apply-error').style.display = 'none';
  document.getElementById('apply-success').style.display = 'none';
}

function closeApplyModal() {
  document.getElementById('modal-apply').style.display = 'none';
}

function openOrdersModal() {
  if (!state.token) {
    openLoginModal();
    return;
  }
  document.getElementById('modal-orders').style.display = 'flex';
  loadOrderHistory();
}

function closeOrdersModal() {
  document.getElementById('modal-orders').style.display = 'none';
}

function closeOrderSuccessModal() {
  document.getElementById('modal-order-success').style.display = 'none';
}

// 2-Step Login Step 1: Verify Password -> Generate OTP
async function handleLoginStep1(event) {
  event.preventDefault();
  const phone = document.getElementById('login-phone').value.trim();
  const password = document.getElementById('login-password').value;
  const errorDiv = document.getElementById('login-step1-error');
  const btn = document.getElementById('btn-login-step1');

  errorDiv.style.display = 'none';
  btn.disabled = true;
  btn.textContent = 'Verifying credentials...';

  try {
    const res = await fetch(`${API_BASE}/customer/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ phoneOrUsername: phone, password })
    });

    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Login failed.');

    state.tempSessionToken = data.tempSessionToken;
    document.getElementById('otp-phone-label').textContent = `OTP code sent to ${data.phoneMasked}`;
    
    // Auto-populate Demo OTP for easy testing
    const demoMatch = data.message.match(/Demo OTP:\s*(\d+)/);
    if (demoMatch) {
      document.getElementById('demo-otp-banner').style.display = 'block';
      document.getElementById('demo-otp-code').textContent = demoMatch[1];
      document.getElementById('login-otp').value = demoMatch[1];
    } else {
      document.getElementById('demo-otp-banner').style.display = 'none';
    }

    document.getElementById('login-step-1').style.display = 'none';
    document.getElementById('login-step-2').style.display = 'block';
  } catch (err) {
    errorDiv.textContent = err.message;
    errorDiv.style.display = 'block';
  } finally {
    btn.disabled = false;
    btn.textContent = 'Verify & Send OTP ➔';
  }
}

// 2-Step Login Step 2: Verify OTP -> Receive JWT
async function handleVerifyOtp(event) {
  event.preventDefault();
  const otpCode = document.getElementById('login-otp').value.trim();
  const errorDiv = document.getElementById('login-step2-error');
  const btn = document.getElementById('btn-verify-otp');

  errorDiv.style.display = 'none';
  btn.disabled = true;
  btn.textContent = 'Verifying OTP...';

  try {
    const res = await fetch(`${API_BASE}/customer/auth/verify-otp`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        tempSessionToken: state.tempSessionToken,
        otpCode
      })
    });

    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'OTP verification failed.');

    state.token = data.token;
    state.customer = {
      customerId: data.customerId,
      shopName: data.shopName,
      ownerName: data.ownerName,
      customerType: data.customerType,
      city: data.city
    };

    localStorage.setItem('casa_customer_token', state.token);
    localStorage.setItem('casa_customer_profile', JSON.stringify(state.customer));

    closeLoginModal();
    renderAuthState();
    loadCatalog();
    showToast(`🎉 Welcome, ${state.customer.shopName}!`);
  } catch (err) {
    errorDiv.textContent = err.message;
    errorDiv.style.display = 'block';
  } finally {
    btn.disabled = false;
    btn.textContent = '✓ Complete Login & Access Portal';
  }
}

function handleLogout() {
  state.token = null;
  state.customer = null;
  state.cart = {};
  localStorage.removeItem('casa_customer_token');
  localStorage.removeItem('casa_customer_profile');
  renderAuthState();
  loadCatalog();
  showToast('Logged out successfully.');
}

// Wholesale Account Application
async function handleApplyAccount(event) {
  event.preventDefault();
  const shopName = document.getElementById('apply-shop').value.trim();
  const ownerName = document.getElementById('apply-owner').value.trim();
  const phone = document.getElementById('apply-phone').value.trim();
  const whatsapp = document.getElementById('apply-whatsapp').value.trim();
  const city = document.getElementById('apply-city').value.trim();
  const gstNumber = document.getElementById('apply-gst').value.trim();
  const address = document.getElementById('apply-address').value.trim();
  const password = document.getElementById('apply-password').value;

  const errorDiv = document.getElementById('apply-error');
  const successDiv = document.getElementById('apply-success');
  const btn = document.getElementById('btn-submit-apply');

  errorDiv.style.display = 'none';
  successDiv.style.display = 'none';
  btn.disabled = true;
  btn.textContent = 'Submitting application...';

  try {
    const res = await fetch(`${API_BASE}/customer/apply`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ shopName, ownerName, phone, whatsapp, city, gstNumber, address, password })
    });

    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Application failed.');

    successDiv.textContent = '🎉 ' + data.message;
    successDiv.style.display = 'block';
    document.getElementById('apply-form').reset();

    setTimeout(() => {
      closeApplyModal();
      openLoginModal();
    }, 2500);
  } catch (err) {
    errorDiv.textContent = err.message;
    errorDiv.style.display = 'block';
  } finally {
    btn.disabled = false;
    btn.textContent = '🚀 Submit Wholesale Application';
  }
}

// =======================================================
// MY ORDERS & 1-CLICK REORDER
// =======================================================

async function loadOrderHistory() {
  const container = document.getElementById('orders-ledger-list');
  container.innerHTML = `
    <div class="loading-state">
      <div class="pulsing-spinner"></div>
      <p>Loading your wholesale orders history...</p>
    </div>
  `;

  try {
    const res = await fetch(`${API_BASE}/customer/orders`, {
      headers: { 'Authorization': `Bearer ${state.token}` }
    });

    if (!res.ok) throw new Error('Failed to load orders.');
    const orders = await res.json();

    if (orders.length === 0) {
      container.innerHTML = `
        <div style="text-align: center; padding: 40px; color: var(--text-muted);">
          <span style="font-size: 40px; display: block; margin-bottom: 8px;">📦</span>
          <p>You haven't placed any wholesale orders yet.</p>
          <button class="btn btn-sm btn-nav-login mt-2" onclick="closeOrdersModal(); scrollToProducts();">Explore Shirts</button>
        </div>
      `;
      return;
    }

    container.innerHTML = orders.map(o => {
      const itemsList = o.items.map(i => `• ${i.productName} (${i.size}/${i.color}) × <strong>${i.quantity} pcs</strong> @ ₹${Number(i.unitPrice).toFixed(2)}`).join('<br>');
      const totalPcs = o.items.reduce((s, i) => s + i.quantity, 0);

      const statusBadge = `<span class="badge ${o.orderStatus === 'PENDING' ? 'badge-warning' : 'badge-success'}">${o.orderStatus}</span>`;

      return `
        <div class="drawer-item-card" style="flex-direction: column; gap: 8px; margin-bottom: 12px;">
          <div style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--border-light); padding-bottom: 8px;">
            <div>
              <strong style="font-size: 14px;">Order #${o.invoiceNo}</strong>
              <div style="font-size: 11px; color: var(--text-muted);">${new Date(o.salesDate).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })}</div>
            </div>
            <div>${statusBadge}</div>
          </div>
          
          <div style="font-size: 12px; color: var(--text-secondary); line-height: 1.5; padding: 4px 0;">
            ${itemsList}
          </div>

          <div style="display: flex; justify-content: space-between; align-items: center; border-top: 1px solid var(--border-light); padding-top: 8px; margin-top: 4px;">
            <div>
              <span style="font-size: 11px; color: var(--text-muted);">Total (${totalPcs} Pcs):</span>
              <strong style="font-size: 15px; color: var(--primary); display: block;">₹${Number(o.finalAmount).toFixed(2)}</strong>
            </div>
            <button class="btn btn-sm btn-nav-apply" onclick="handleReorder(${o.id})">
              🔄 1-Click Reorder
            </button>
          </div>
        </div>
      `;
    }).join('');
  } catch (err) {
    container.innerHTML = `
      <div style="text-align: center; padding: 30px; color: #ef4444;">
        <p>⚠️ ${err.message}</p>
      </div>
    `;
  }
}

async function handleReorder(orderId) {
  try {
    const res = await fetch(`${API_BASE}/customer/orders/${orderId}/reorder`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${state.token}` }
    });

    if (!res.ok) throw new Error('Failed to prepare reorder cart.');
    const reorderData = await res.json();

    state.cart = {};
    for (const item of reorderData.items) {
      const product = state.products.find(p => p.id === item.productId);
      if (product && product.available) {
        state.cart[product.id] = { product, quantity: item.quantity };
      }
    }

    updateCartBadges();
    closeOrdersModal();
    openCartDrawer();
    showToast('✓ Cart populated with previous order!');
  } catch (err) {
    alert('Reorder Notice: ' + err.message);
  }
}
