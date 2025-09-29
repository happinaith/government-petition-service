import React from 'react';
import './PetitionCard.css';

const PetitionCard = ({ petition, onSign, onViewDetails }) => {
  const formatDate = (dateString) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  };

  const getStatusColor = (status) => {
    switch (status) {
      case 'Active': return '#28a745';
      case 'Under Review': return '#ffc107';
      case 'Closed': return '#dc3545';
      default: return '#6c757d';
    }
  };

  return (
    <div className="petition-card">
      <div className="petition-header">
        <h3 className="petition-title">{petition.title}</h3>
        <div className="petition-meta">
          <span 
            className="petition-status" 
            style={{ backgroundColor: getStatusColor(petition.status) }}
          >
            {petition.status}
          </span>
          <span className="petition-category">{petition.category}</span>
        </div>
      </div>
      
      <p className="petition-description">
        {petition.description.length > 150 
          ? `${petition.description.substring(0, 150)}...` 
          : petition.description}
      </p>
      
      <div className="petition-details">
        <div className="detail-row">
          <span className="label">Theme:</span>
          <span className="value">{petition.theme}</span>
        </div>
        <div className="detail-row">
          <span className="label">Target:</span>
          <span className="value">{petition.targetGovernmentLevel}</span>
        </div>
        <div className="detail-row">
          <span className="label">Created by:</span>
          <span className="value">{petition.createdBy}</span>
        </div>
        <div className="detail-row">
          <span className="label">Created:</span>
          <span className="value">{formatDate(petition.createdDate)}</span>
        </div>
      </div>
      
      <div className="petition-signatures">
        <strong>{petition.signatureCount.toLocaleString()} signatures</strong>
      </div>
      
      <div className="petition-actions">
        <button 
          className="btn btn-primary" 
          onClick={() => onSign(petition.id)}
          disabled={petition.status !== 'Active'}
        >
          Sign Petition
        </button>
        <button 
          className="btn btn-secondary" 
          onClick={() => onViewDetails(petition)}
        >
          View Details
        </button>
      </div>
    </div>
  );
};

export default PetitionCard;