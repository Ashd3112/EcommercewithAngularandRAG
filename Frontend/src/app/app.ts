import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EcommerceService, Product } from './services/ecommerce.service';

interface CartItem {
  product: Product;
  quantity: number;
}

interface ChatMessage {
  sender: 'user' | 'bot';
  text: string;
  retrievedProducts?: Product[];
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  private readonly ecommerceService = inject(EcommerceService);

  // E-commerce state
  products = signal<Product[]>([]);
  searchQuery = signal<string>('');
  selectedCategory = signal<string>('All');
  maxPriceFilter = signal<number>(300);
  cart = signal<CartItem[]>([]);
  isCartOpen = signal<boolean>(false);

  // RAG Chat Assistant state
  isChatOpen = signal<boolean>(false);
  chatInput = signal<string>('');
  chatMessages = signal<ChatMessage[]>([
    {
      sender: 'bot',
      text: "Hello! I am Aura, your AI shopping assistant. Ask me questions about our product features, recommendation guides, or budget searches (e.g. 'Show me gadgets under $100' or 'recommend a lighting setting')."
    }
  ]);
  isTyping = signal<boolean>(false);
  suggestedChips = signal<string[]>([
    'Recommend a keyboard under $150',
    'What lighting products do you have?',
    'Show the cheapest product',
    'Which product is rated highest?'
  ]);

  // Computed state
  categories = computed(() => {
    const list = this.products().map(p => p.category);
    return ['All', ...Array.from(new Set(list))];
  });

  filteredProducts = computed(() => {
    let list = this.products();

    // Category filter
    if (this.selectedCategory() !== 'All') {
      list = list.filter(p => p.category === this.selectedCategory());
    }

    // Price filter
    list = list.filter(p => p.price <= this.maxPriceFilter());

    // Search query filter (client-side backup)
    if (this.searchQuery().trim()) {
      const q = this.searchQuery().toLowerCase();
      list = list.filter(p => 
        p.name.toLowerCase().includes(q) || 
        p.description.toLowerCase().includes(q) || 
        p.category.toLowerCase().includes(q)
      );
    }

    return list;
  });

  cartCount = computed(() => {
    return this.cart().reduce((sum, item) => sum + item.quantity, 0);
  });

  cartTotal = computed(() => {
    return this.cart().reduce((sum, item) => sum + (item.product.price * item.quantity), 0);
  });

  ngOnInit() {
    this.loadProducts();
  }

  loadProducts() {
    this.ecommerceService.getProducts().subscribe({
      next: (data) => {
        this.products.set(data);
        // Pre-set slider max price to the highest product price
        if (data.length > 0) {
          const maxPrice = Math.max(...data.map(p => p.price));
          this.maxPriceFilter.set(Math.ceil(maxPrice));
        }
      },
      error: (err) => {
        console.error('Failed to load products from API. Falling back to local data.', err);
        // Fallback seed data in case API is running on a different port or database is not started yet
        const mockFallback: Product[] = [
          { id: 1, name: 'Vortex Noise Cancelling Headphones', description: 'Premium over-ear wireless headphones with active noise cancellation, 40-hour battery life.', category: 'Electronics', price: 199.99, imageUrl: 'https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=500', rating: 4.8, stock: 25 },
          { id: 2, name: 'Aura Smart Ambient Light', description: 'RGB LED smart lamp syncing with music and screen. Features 16 million colors.', category: 'Smart Home', price: 49.99, imageUrl: 'https://images.unsplash.com/photo-1507646227500-4d389b0012be?w=500', rating: 4.5, stock: 50 },
          { id: 3, name: 'Nebula Mechanical Keyboard', description: 'Tenkeyless mechanical keyboard with linear yellow switches, double-shot PBT keycaps.', category: 'Electronics', price: 129.99, imageUrl: 'https://images.unsplash.com/photo-1587829741301-dc798b83add3?w=500', rating: 4.7, stock: 15 }
        ];
        this.products.set(mockFallback);
      }
    });
  }

  // Search logic
  onSearch() {
    const query = this.searchQuery().trim();
    if (query) {
      this.ecommerceService.searchProducts(query).subscribe({
        next: (data) => {
          // If we find matches, update the display
          if (data.length > 0) {
            this.products.set(data);
          }
        },
        error: (err) => console.error(err)
      });
    } else {
      this.loadProducts();
    }
  }

  clearSearch() {
    this.searchQuery.set('');
    this.loadProducts();
  }

  selectCategory(category: string) {
    this.selectedCategory.set(category);
  }

  // Cart operations
  addToCart(product: Product) {
    this.cart.update(current => {
      const existing = current.find(item => item.product.id === product.id);
      if (existing) {
        return current.map(item => 
          item.product.id === product.id 
            ? { ...item, quantity: item.quantity + 1 } 
            : item
        );
      } else {
        return [...current, { product, quantity: 1 }];
      }
    });
    // Visual feedback - slide open cart
    this.isCartOpen.set(true);
  }

  updateQuantity(productId: number, change: number) {
    this.cart.update(current => {
      return current.map(item => {
        if (item.product.id === productId) {
          const newQty = item.quantity + change;
          return newQty > 0 ? { ...item, quantity: newQty } : null;
        }
        return item;
      }).filter((item): item is CartItem => item !== null);
    });
  }

  removeFromCart(productId: number) {
    this.cart.update(current => current.filter(item => item.product.id !== productId));
  }

  checkout() {
    alert('Thank you for your purchase! Checkout completed successfully.');
    this.cart.set([]);
    this.isCartOpen.set(false);
  }

  // Chat/RAG operations
  toggleChat() {
    this.isChatOpen.update(val => !val);
  }

  selectSuggestionChip(chipText: string) {
    this.chatInput.set(chipText);
    this.sendChatMessage();
  }

  sendChatMessage() {
    const text = this.chatInput().trim();
    if (!text) return;

    // Add user message
    this.chatMessages.update(msgs => [...msgs, { sender: 'user', text }]);
    this.chatInput.set('');
    this.isTyping.set(true);

    // Call .NET API
    this.ecommerceService.sendChatMessage(text).subscribe({
      next: (res) => {
        this.isTyping.set(false);
        this.chatMessages.update(msgs => [...msgs, {
          sender: 'bot',
          text: res.response,
          retrievedProducts: res.retrievedProducts
        }]);
        this.scrollToBottom();
      },
      error: (err) => {
        console.error(err);
        this.isTyping.set(false);
        // Fallback local mock intelligence logic
        setTimeout(() => {
          this.chatMessages.update(msgs => [...msgs, {
            sender: 'bot',
            text: "Sorry, I am having trouble reaching the AI service right now. Please verify the .NET backend is running on http://localhost:5000."
          }]);
          this.scrollToBottom();
        }, 1000);
      }
    });
  }

  // Helper method to format text to Markdown-like HTML
  formatMessage(text: string): string {
    if (!text) return '';
    let formatted = text
      .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
      .replace(/\*([^*]+)\*/g, '<em>$1</em>')
      .replace(/###\s+([^\n]+)/g, '<h3>$1</h3>')
      .replace(/-\s+([^\n]+)/g, '<li>$1</li>');

    // Wrap list items
    if (formatted.includes('<li>')) {
      // Very basic regex list wrapper
      formatted = formatted.replace(/(<li>.*<\/li>)/gs, '<ul>$1</ul>');
    }

    return formatted;
  }

  private scrollToBottom() {
    setTimeout(() => {
      const element = document.querySelector('.chat-messages');
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    }, 100);
  }
}
