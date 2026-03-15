export interface Product {
  id: string;
  name: string;
  description: string;
  price: number;
  costPrice?: number;
  supplier?: string;
  era: string;
  category: '70s' | '80s' | '90s' | 'y2k' | 'modern';
  size: '6' | '8' | '10' | '12' | '14' | '16';
  condition: 'mint' | 'excellent' | 'good' | 'fair';
  imageUrl: string;
  inStock: boolean;
}

export interface CartItem {
  product: Product;
  quantity: number;
}
