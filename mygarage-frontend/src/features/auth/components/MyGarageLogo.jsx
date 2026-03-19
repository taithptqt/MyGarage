import React from 'react';

const MyGarageLogo = ({ className }) => {
  return (
    <div className={`logo-container ${className}`} style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '24px' }}>
      <div style={{ 
        display: 'flex', 
        alignItems: 'center', 
        fontSize: '32px', 
        fontWeight: '800', 
        letterSpacing: '-1px',
        color: '#1a202c'
      }}>
        <span style={{ color: '#2b6eff' }}>My</span>
        <span>Garage</span>
        <div style={{ 
          marginLeft: '4px',
          width: '8px',
          height: '8px',
          borderRadius: '50%',
          backgroundColor: '#ff4d4f',
          marginTop: '12px'
        }}></div>
      </div>
    </div>
  );
};

export default MyGarageLogo;
