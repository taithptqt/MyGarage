import React, { useState, useCallback } from 'react';
import Cropper from 'react-easy-crop';

export const getCroppedImg = async (imageSrc, pixelCrop) => {
  const image = await new Promise((resolve, reject) => {
    const img = new Image();
    img.addEventListener('load', () => resolve(img));
    img.addEventListener('error', (error) => reject(error));
    // Remove crossOrigin for local data URLs to avoid potential issues
    img.src = imageSrc;
  });

  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d');

  if (!ctx) return null;

  // Set output size to a standard high-quality avatar size (e.g. 400x400)
  const safeOutputSize = 400;
  canvas.width = safeOutputSize;
  canvas.height = safeOutputSize;

  ctx.drawImage(
    image,
    pixelCrop.x,
    pixelCrop.y,
    pixelCrop.width,
    pixelCrop.height,
    0,
    0,
    safeOutputSize,
    safeOutputSize
  );

  return new Promise((resolve) => {
    canvas.toBlob((blob) => {
      resolve(blob);
    }, 'image/jpeg');
  });
};

export default function ImageCropper({ image, onCropComplete, onCancel }) {
  const [crop, setCrop] = useState({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [croppedAreaPixels, setCroppedAreaPixels] = useState(null);

  const onCropChange = (crop) => setCrop(crop);
  const onZoomChange = (zoom) => setZoom(zoom);

  const onCropCompleteInternal = useCallback((_croppedArea, croppedAreaPixels) => {
    setCroppedAreaPixels(croppedAreaPixels);
  }, []);

  const handleSave = async () => {
    try {
      if (!croppedAreaPixels) return;
      const croppedImageBlob = await getCroppedImg(image, croppedAreaPixels);
      onCropComplete(croppedImageBlob);
    } catch (e) {
      console.error("Crop error:", e);
      alert("Có lđ-i xảy ra khi cắt ảnh. Vui lòng thử lại.");
    }
  };

  return (
    <div className="image-cropper-overlay" style={{
      position: 'fixed',
      inset: 0,
      backgroundColor: 'rgba(0,0,0,0.85)',
      zIndex: 9999, /* Ensure it's on top of everything */
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      padding: '24px'
    }}>
      <div style={{ 
        position: 'relative', 
        width: '100%', 
        maxWidth: '500px', 
        height: '450px', 
        background: '#1a1a1a', 
        borderRadius: '16px', 
        overflow: 'hidden',
        boxShadow: '0 20px 50px rgba(0,0,0,0.5)'
      }}>
        <Cropper
          image={image}
          crop={crop}
          zoom={zoom}
          aspect={1}
          cropShape="round"
          showGrid={false}
          onCropChange={onCropChange}
          onCropComplete={onCropCompleteInternal}
          onZoomChange={onZoomChange}
        />
      </div>
      <div style={{ marginTop: '20px', display: 'flex', gap: '12px' }}>
        <button className="btn-ghost" onClick={onCancel} style={{ color: '#fff', borderColor: '#fff' }}>Hủy</button>
        <button className="btn-primary" onClick={handleSave}>Cắt & Lưu</button>
      </div>
    </div>
  );
}


