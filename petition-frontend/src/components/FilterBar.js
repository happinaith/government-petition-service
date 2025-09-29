import React, { useState } from 'react';
import './FilterBar.css';

const FilterBar = ({ 
  categories, 
  themes, 
  filters, 
  onFiltersChange,
  onSearch,
  searchQuery,
  onSearchChange 
}) => {
  const [isExpanded, setIsExpanded] = useState(false);

  const handleFilterChange = (filterType, value) => {
    const newFilters = {
      ...filters,
      [filterType]: value === '' ? undefined : value
    };
    onFiltersChange(newFilters);
  };

  const clearFilters = () => {
    onFiltersChange({});
    onSearchChange('');
  };

  const governmentLevels = ['Federal', 'State', 'Local'];
  const statusOptions = ['Active', 'Under Review', 'Closed'];

  return (
    <div className="filter-bar">
      <div className="search-section">
        <div className="search-input-group">
          <input
            type="text"
            placeholder="Search petitions by title or description..."
            value={searchQuery}
            onChange={(e) => onSearchChange(e.target.value)}
            className="search-input"
            onKeyPress={(e) => e.key === 'Enter' && onSearch()}
          />
          <button onClick={onSearch} className="search-btn">
            Search
          </button>
        </div>
        
        <button 
          className="filter-toggle"
          onClick={() => setIsExpanded(!isExpanded)}
        >
          {isExpanded ? 'Hide Filters' : 'Show Filters'} 
          <span className={`arrow ${isExpanded ? 'up' : 'down'}`}>▼</span>
        </button>
      </div>

      {isExpanded && (
        <div className="filters-section">
          <div className="filters-grid">
            <div className="filter-group">
              <label>Category</label>
              <select 
                value={filters.category || ''} 
                onChange={(e) => handleFilterChange('category', e.target.value)}
              >
                <option value="">All Categories</option>
                {categories.map(category => (
                  <option key={category} value={category}>{category}</option>
                ))}
              </select>
            </div>

            <div className="filter-group">
              <label>Theme</label>
              <select 
                value={filters.theme || ''} 
                onChange={(e) => handleFilterChange('theme', e.target.value)}
              >
                <option value="">All Themes</option>
                {themes.map(theme => (
                  <option key={theme} value={theme}>{theme}</option>
                ))}
              </select>
            </div>

            <div className="filter-group">
              <label>Status</label>
              <select 
                value={filters.status || ''} 
                onChange={(e) => handleFilterChange('status', e.target.value)}
              >
                <option value="">All Status</option>
                {statusOptions.map(status => (
                  <option key={status} value={status}>{status}</option>
                ))}
              </select>
            </div>

            <div className="filter-group">
              <label>Government Level</label>
              <select 
                value={filters.targetGovernmentLevel || ''} 
                onChange={(e) => handleFilterChange('targetGovernmentLevel', e.target.value)}
              >
                <option value="">All Levels</option>
                {governmentLevels.map(level => (
                  <option key={level} value={level}>{level}</option>
                ))}
              </select>
            </div>
          </div>
          
          <div className="filter-actions">
            <button onClick={clearFilters} className="clear-filters-btn">
              Clear All Filters
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

export default FilterBar;