import React, { useState, useEffect } from 'react';
import './App.css';
import PetitionCard from './components/PetitionCard';
import FilterBar from './components/FilterBar';
import CreatePetitionModal from './components/CreatePetitionModal';
import { petitionService } from './services/petitionService';

function App() {
  const [petitions, setPetitions] = useState([]);
  const [categories, setCategories] = useState([]);
  const [themes, setThemes] = useState([]);
  const [filters, setFilters] = useState({});
  const [searchQuery, setSearchQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [successMessage, setSuccessMessage] = useState('');

  useEffect(() => {
    loadInitialData();
  }, []);

  const loadInitialData = async () => {
    setLoading(true);
    try {
      const [petitionsData, categoriesData, themesData] = await Promise.all([
        petitionService.getPetitions(),
        petitionService.getCategories(),
        petitionService.getThemes()
      ]);
      
      setPetitions(petitionsData);
      setCategories(categoriesData);
      setThemes(themesData);
      setError('');
    } catch (err) {
      setError('Failed to load data. Please make sure the API is running on http://localhost:5027');
      console.error('Error loading data:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = async () => {
    setLoading(true);
    try {
      const searchFilters = { ...filters };
      if (searchQuery.trim()) {
        searchFilters.searchQuery = searchQuery.trim();
      }
      
      const petitionsData = await petitionService.getPetitions(searchFilters);
      setPetitions(petitionsData);
      setError('');
    } catch (err) {
      setError('Failed to search petitions');
      console.error('Error searching:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleFiltersChange = async (newFilters) => {
    setFilters(newFilters);
    setLoading(true);
    try {
      const searchFilters = { ...newFilters };
      if (searchQuery.trim()) {
        searchFilters.searchQuery = searchQuery.trim();
      }
      
      const petitionsData = await petitionService.getPetitions(searchFilters);
      setPetitions(petitionsData);
      setError('');
    } catch (err) {
      setError('Failed to filter petitions');
      console.error('Error filtering:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleSignPetition = async (petitionId) => {
    try {
      await petitionService.signPetition(petitionId);
      
      // Update the petition in the list
      setPetitions(prev => 
        prev.map(p => 
          p.id === petitionId 
            ? { ...p, signatureCount: p.signatureCount + 1 }
            : p
        )
      );
      
      showSuccessMessage('Successfully signed the petition!');
    } catch (err) {
      setError('Failed to sign petition');
      console.error('Error signing petition:', err);
    }
  };

  const handleViewDetails = (petition) => {
    // In a real app, this would open a detailed view or navigate to a detail page
    alert(`Petition Details:\n\nTitle: ${petition.title}\n\nDescription: ${petition.description}\n\nCreated by: ${petition.createdBy}\nSignatures: ${petition.signatureCount}\nStatus: ${petition.status}`);
  };

  const handleCreatePetition = async (petitionData) => {
    try {
      const newPetition = await petitionService.createPetition(petitionData);
      setPetitions(prev => [newPetition, ...prev]);
      setIsModalOpen(false);
      showSuccessMessage('Petition created successfully!');
    } catch (err) {
      setError('Failed to create petition');
      console.error('Error creating petition:', err);
    }
  };

  const showSuccessMessage = (message) => {
    setSuccessMessage(message);
    setTimeout(() => setSuccessMessage(''), 5000);
  };

  return (
    <div className="App">
      <header className="app-header">
        <div className="container">
          <h1>Government Petition Service</h1>
          <p>Make your voice heard - Create and support petitions for government action</p>
          <button 
            className="create-petition-btn"
            onClick={() => setIsModalOpen(true)}
          >
            + Create New Petition
          </button>
        </div>
      </header>

      <main className="container">
        {error && (
          <div className="error-banner">
            {error}
            <button onClick={() => setError('')}>×</button>
          </div>
        )}

        {successMessage && (
          <div className="success-banner">
            {successMessage}
            <button onClick={() => setSuccessMessage('')}>×</button>
          </div>
        )}

        <FilterBar
          categories={categories}
          themes={themes}
          filters={filters}
          onFiltersChange={handleFiltersChange}
          onSearch={handleSearch}
          searchQuery={searchQuery}
          onSearchChange={setSearchQuery}
        />

        {loading ? (
          <div className="loading">
            <div className="loading-spinner"></div>
            <p>Loading petitions...</p>
          </div>
        ) : petitions.length > 0 ? (
          <div className="petitions-grid">
            {petitions.map(petition => (
              <PetitionCard
                key={petition.id}
                petition={petition}
                onSign={handleSignPetition}
                onViewDetails={handleViewDetails}
              />
            ))}
          </div>
        ) : (
          <div className="no-results">
            <h3>No petitions found</h3>
            <p>Try adjusting your search criteria or create a new petition.</p>
          </div>
        )}
      </main>

      <CreatePetitionModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSubmit={handleCreatePetition}
        categories={categories}
        themes={themes}
      />
    </div>
  );
}

export default App;
