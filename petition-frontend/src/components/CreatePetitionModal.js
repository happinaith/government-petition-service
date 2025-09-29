import React, { useState } from 'react';
import './CreatePetitionModal.css';

const CreatePetitionModal = ({ 
  isOpen, 
  onClose, 
  onSubmit, 
  categories, 
  themes 
}) => {
  const [formData, setFormData] = useState({
    title: '',
    description: '',
    category: '',
    theme: '',
    createdBy: '',
    targetGovernmentLevel: ''
  });
  
  const [errors, setErrors] = useState({});

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: value
    }));
    
    // Clear error for this field when user starts typing
    if (errors[name]) {
      setErrors(prev => ({
        ...prev,
        [name]: ''
      }));
    }
  };

  const validateForm = () => {
    const newErrors = {};
    
    if (!formData.title.trim()) {
      newErrors.title = 'Title is required';
    } else if (formData.title.length < 10) {
      newErrors.title = 'Title must be at least 10 characters long';
    }
    
    if (!formData.description.trim()) {
      newErrors.description = 'Description is required';
    } else if (formData.description.length < 50) {
      newErrors.description = 'Description must be at least 50 characters long';
    }
    
    if (!formData.category) {
      newErrors.category = 'Category is required';
    }
    
    if (!formData.theme) {
      newErrors.theme = 'Theme is required';
    }
    
    if (!formData.createdBy.trim()) {
      newErrors.createdBy = 'Your name is required';
    }
    
    if (!formData.targetGovernmentLevel) {
      newErrors.targetGovernmentLevel = 'Target government level is required';
    }
    
    return newErrors;
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    
    const newErrors = validateForm();
    if (Object.keys(newErrors).length > 0) {
      setErrors(newErrors);
      return;
    }
    
    onSubmit(formData);
    
    // Reset form
    setFormData({
      title: '',
      description: '',
      category: '',
      theme: '',
      createdBy: '',
      targetGovernmentLevel: ''
    });
    setErrors({});
  };

  const handleClose = () => {
    setFormData({
      title: '',
      description: '',
      category: '',
      theme: '',
      createdBy: '',
      targetGovernmentLevel: ''
    });
    setErrors({});
    onClose();
  };

  if (!isOpen) return null;

  const governmentLevels = ['Federal', 'State', 'Local'];

  return (
    <div className="modal-overlay" onClick={handleClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>Create New Petition</h2>
          <button className="close-btn" onClick={handleClose}>×</button>
        </div>
        
        <form onSubmit={handleSubmit} className="petition-form">
          <div className="form-group">
            <label htmlFor="title">Petition Title *</label>
            <input
              type="text"
              id="title"
              name="title"
              value={formData.title}
              onChange={handleChange}
              placeholder="Enter a clear, compelling title for your petition"
              className={errors.title ? 'error' : ''}
            />
            {errors.title && <span className="error-message">{errors.title}</span>}
          </div>

          <div className="form-group">
            <label htmlFor="description">Description *</label>
            <textarea
              id="description"
              name="description"
              value={formData.description}
              onChange={handleChange}
              placeholder="Provide a detailed description of what you're petitioning for and why it matters"
              rows="5"
              className={errors.description ? 'error' : ''}
            />
            {errors.description && <span className="error-message">{errors.description}</span>}
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="category">Category *</label>
              <select
                id="category"
                name="category"
                value={formData.category}
                onChange={handleChange}
                className={errors.category ? 'error' : ''}
              >
                <option value="">Select a category</option>
                {categories.map(category => (
                  <option key={category} value={category}>{category}</option>
                ))}
                <option value="Other">Other</option>
              </select>
              {errors.category && <span className="error-message">{errors.category}</span>}
            </div>

            <div className="form-group">
              <label htmlFor="theme">Theme *</label>
              <select
                id="theme"
                name="theme"
                value={formData.theme}
                onChange={handleChange}
                className={errors.theme ? 'error' : ''}
              >
                <option value="">Select a theme</option>
                {themes.map(theme => (
                  <option key={theme} value={theme}>{theme}</option>
                ))}
                <option value="Other">Other</option>
              </select>
              {errors.theme && <span className="error-message">{errors.theme}</span>}
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="createdBy">Your Name *</label>
              <input
                type="text"
                id="createdBy"
                name="createdBy"
                value={formData.createdBy}
                onChange={handleChange}
                placeholder="Enter your full name"
                className={errors.createdBy ? 'error' : ''}
              />
              {errors.createdBy && <span className="error-message">{errors.createdBy}</span>}
            </div>

            <div className="form-group">
              <label htmlFor="targetGovernmentLevel">Target Government Level *</label>
              <select
                id="targetGovernmentLevel"
                name="targetGovernmentLevel"
                value={formData.targetGovernmentLevel}
                onChange={handleChange}
                className={errors.targetGovernmentLevel ? 'error' : ''}
              >
                <option value="">Select target level</option>
                {governmentLevels.map(level => (
                  <option key={level} value={level}>{level}</option>
                ))}
              </select>
              {errors.targetGovernmentLevel && <span className="error-message">{errors.targetGovernmentLevel}</span>}
            </div>
          </div>

          <div className="form-actions">
            <button type="button" onClick={handleClose} className="btn btn-cancel">
              Cancel
            </button>
            <button type="submit" className="btn btn-create">
              Create Petition
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default CreatePetitionModal;