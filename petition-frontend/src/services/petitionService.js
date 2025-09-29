const API_BASE_URL = 'http://localhost:5027/api';

export const petitionService = {
  // Get all petitions with optional filters
  async getPetitions(filters = {}) {
    const queryParams = new URLSearchParams();
    
    Object.keys(filters).forEach(key => {
      if (filters[key] && filters[key] !== '') {
        queryParams.append(key, filters[key]);
      }
    });

    const url = `${API_BASE_URL}/petitions${queryParams.toString() ? `?${queryParams.toString()}` : ''}`;
    const response = await fetch(url);
    
    if (!response.ok) {
      throw new Error('Failed to fetch petitions');
    }
    
    return response.json();
  },

  // Get a single petition by ID
  async getPetitionById(id) {
    const response = await fetch(`${API_BASE_URL}/petitions/${id}`);
    
    if (!response.ok) {
      throw new Error('Failed to fetch petition');
    }
    
    return response.json();
  },

  // Create a new petition
  async createPetition(petitionData) {
    const response = await fetch(`${API_BASE_URL}/petitions`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(petitionData),
    });
    
    if (!response.ok) {
      throw new Error('Failed to create petition');
    }
    
    return response.json();
  },

  // Sign a petition
  async signPetition(id) {
    const response = await fetch(`${API_BASE_URL}/petitions/${id}/sign`, {
      method: 'POST',
    });
    
    if (!response.ok) {
      throw new Error('Failed to sign petition');
    }
    
    return response.json();
  },

  // Get available categories
  async getCategories() {
    const response = await fetch(`${API_BASE_URL}/petitions/categories`);
    
    if (!response.ok) {
      throw new Error('Failed to fetch categories');
    }
    
    return response.json();
  },

  // Get available themes
  async getThemes() {
    const response = await fetch(`${API_BASE_URL}/petitions/themes`);
    
    if (!response.ok) {
      throw new Error('Failed to fetch themes');
    }
    
    return response.json();
  },
};