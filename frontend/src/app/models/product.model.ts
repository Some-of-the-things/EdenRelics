export interface Product {
  id: string;
  name: string;
  description: string;
  price: number;
  salePrice?: number | null;
  costPrice?: number;
  supplier?: string;
  era: string;
  category: '50s' | '60s' | '70s' | '80s' | '90s' | 'y2k' | 'modern';
  size: '6' | '6/8' | '8' | '8/10' | '10' | '10/12' | '12' | '12/14' | '14' | '14/16' | '16';
  condition: 'mint' | 'excellent' | 'very good' | 'good' | 'fair';
  imageUrl: string;
  additionalImageUrls?: string[];
  inStock: boolean;
  viewCount?: number;
}

export interface CartItem {
  product: Product;
  quantity: number;
}
