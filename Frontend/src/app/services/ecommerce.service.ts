import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Product {
  id: number;
  name: string;
  description: string;
  category: string;
  price: number;
  imageUrl: string;
  rating: number;
  stock: number;
  similarityScore?: number;
}

export interface ChatResponse {
  response: string;
  retrievedProducts: Product[];
}

@Injectable({
  providedIn: 'root'
})
export class EcommerceService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = 'http://localhost:5194/api'; // Updated to match .NET launchSettings port

  getProducts(category?: string): Observable<Product[]> {
    let url = `${this.apiUrl}/products`;
    if (category) {
      url += `?category=${encodeURIComponent(category)}`;
    }
    return this.http.get<Product[]>(url);
  }

  searchProducts(query: string): Observable<Product[]> {
    return this.http.get<Product[]>(`${this.apiUrl}/products/search?q=${encodeURIComponent(query)}`);
  }

  sendChatMessage(message: string): Observable<ChatResponse> {
    return this.http.post<ChatResponse>(`${this.apiUrl}/chat`, { message });
  }
}
