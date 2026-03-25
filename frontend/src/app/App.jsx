import React from 'react';

const App = () => {
  // Các mã màu chủ đạo:
  // Beige: #DED8CD
  // Dark Green: #1A2C1C
  // Gold: #B8975A

  return (
    <div className="min-h-screen w-full bg-[#DED8CD] text-[#1A2C1C] font-sans selection:bg-[#B8975A] selection:text-white overflow-x-hidden">
      
      {/* 1. HERO SECTION */}
      <header className="relative h-screen w-full flex flex-col items-center justify-between py-16 text-white">
        {/* Background Image with Dark Overlay */}
        <div className="absolute inset-0 z-0">
          <img 
            src="https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&q=80&w=1920" 
            className="w-full h-full object-cover brightness-[0.4]"
            alt="Hero Car"
          />
        </div>
        
        {/* Logo Text */}
        <div className="relative z-10 text-center uppercase tracking-[0.6em] text-xs font-light">
          My <br /> Garage
        </div>

        {/* Main Title */}
        <h1 className="relative z-10 text-4xl md:text-7xl font-serif tracking-[0.25em] text-center px-4 leading-tight">
          WELCOME TO <br className="hidden md:block" /> MY GARAGE
        </h1>

        {/* Scroll Indicator */}
        <div className="relative z-10 flex flex-col items-center gap-2">
          <span className="text-[10px] tracking-[0.4em] uppercase opacity-60">Scroll Down</span>
          <div className="w-[1px] h-12 bg-white/30 animate-pulse"></div>
        </div>
      </header>

      {/* 2. INTRODUCTION SECTION */}
      <section className="py-32 px-6 max-w-4xl mx-auto text-center">
        <h2 className="text-3xl md:text-5xl font-serif tracking-[0.15em] mb-12 uppercase leading-tight">
          A Sanctuary for Motor <br /> Connoisseurs
        </h2>
        <div className="space-y-8 text-sm md:text-base leading-[2] opacity-80 italic max-w-2xl mx-auto">
          <p>MY GARAGE là trung tâm trải nghiệm xe hơi độc đáo, nơi niềm đam mê và sự tinh tế hội ngộ. Chúng tôi không chỉ cung cấp xe, chúng tôi chia sẻ một phong cách sống.</p>
          
          <div className="py-8">
             <p className="not-italic font-serif text-2xl md:text-3xl text-[#1A2C1C] mb-4">
               "Đó không chỉ là một chiếc xe, đó là một tác phẩm nghệ thuật chuyển động."
             </p>
             <div className="flex justify-center mt-6">
                <img 
                  src="https://upload.wikimedia.org/wikipedia/commons/d/d4/Signature_of_John_Hancock.svg" 
                  alt="Signature" 
                  className="w-32 opacity-30 grayscale invert" 
                />
             </div>
          </div>

          <p className="not-italic">
            Từ những dòng xe cổ điển đến những siêu phẩm hiện đại, MY GARAGE cam kết mang đến dịch vụ chăm sóc tận tâm nhất cho từng khách hàng.
          </p>
        </div>
      </section>

      {/* 3. SALES & SERVICE SECTION */}
      <div className="w-full space-y-0">
        
        {/* Sales Segment */}
        <section className="grid md:grid-cols-2 items-stretch min-h-[600px]">
          <div className="p-12 md:p-24 flex flex-col justify-center bg-[#DED8CD] order-2 md:order-1">
            <h2 className="text-4xl font-serif tracking-[0.2em] mb-8 uppercase">Sales</h2>
            <p className="opacity-70 leading-loose text-sm max-w-md">
              Khám phá bộ sưu tập những chiếc xe được tuyển chọn khắt khe nhất, từ những mẫu xe sang trọng bậc nhất đến những chiếc xe thể thao hiệu suất cao hiếm có trên thị trường.
            </p>
            <button className="mt-10 border-b border-[#1A2C1C] w-max pb-2 text-[10px] tracking-[0.3em] uppercase hover:opacity-50 transition">
              View Collection
            </button>
          </div>
          <div className="relative overflow-hidden order-1 md:order-2 h-[400px] md:h-auto">
            <img 
              src="https://images.unsplash.com/photo-1555215695-3004980ad54e?auto=format&fit=crop&q=80&w=1000" 
              className="absolute inset-0 w-full h-full object-cover hover:scale-105 transition duration-1000" 
              alt="Sales Car"
            />
          </div>
        </section>

        {/* Service Segment */}
        <section className="grid md:grid-cols-2 items-stretch min-h-[600px]">
          <div className="relative overflow-hidden h-[400px] md:h-auto">
            <img 
              src="https://images.unsplash.com/photo-1486006396193-471c6f58c63b?auto=format&fit=crop&q=80&w=1000" 
              className="absolute inset-0 w-full h-full object-cover hover:scale-105 transition duration-1000" 
              alt="Service"
            />
          </div>
          <div className="p-12 md:p-24 flex flex-col justify-center bg-[#DED8CD]">
            <h2 className="text-4xl font-serif tracking-[0.2em] mb-8 uppercase">Service</h2>
            <p className="opacity-70 leading-loose text-sm max-w-md">
              Đội ngũ kỹ thuật viên tay nghề cao của chúng tôi luôn sẵn sàng bảo trì và tối ưu hóa hiệu suất cho chiếc xe của bạn với trang thiết bị đạt tiêu chuẩn quốc tế.
            </p>
            <button className="mt-10 border-b border-[#1A2C1C] w-max pb-2 text-[10px] tracking-[0.3em] uppercase hover:opacity-50 transition">
              Our Services
            </button>
          </div>
        </section>

      </div>

      {/* 4. GOLD SEARCH SECTION */}
      <section className="bg-[#B8975A] py-24 text-white text-center flex flex-col items-center">
        <div className="text-4xl font-light mb-8 opacity-40">×</div>
        <div className="relative w-full max-w-sm px-6">
          <input 
            type="text" 
            placeholder="SEARCH OUR COLLECTION" 
            className="w-full bg-transparent border-b border-white/40 pb-4 text-center outline-none uppercase text-[11px] tracking-[0.4em] placeholder:text-white/50 focus:border-white transition"
          />
        </div>
      </section>

      {/* 5. LARGE IMAGE / SOCIAL PREVIEW */}
      <section className="w-full bg-[#DED8CD] py-12 px-4 md:px-12">
        <div className="max-w-7xl mx-auto relative group overflow-hidden shadow-2xl">
           <img 
            src="https://images.unsplash.com/photo-1566492031773-4f4e44671857?auto=format&fit=crop&q=80&w=1600" 
            className="w-full h-[500px] md:h-[800px] object-cover grayscale brightness-75 group-hover:grayscale-0 group-hover:brightness-100 transition duration-1000"
            alt="Garage Professional"
           />
           <div className="absolute inset-0 bg-[#1A2C1C]/20 flex items-center justify-center">
              <h2 className="text-white text-4xl md:text-6xl font-serif tracking-[0.3em] uppercase">Social</h2>
           </div>
        </div>
      </section>

      {/* 6. CONTACT SECTION */}
      <section className="bg-[#1A2C1C] text-white py-32 px-6 md:px-20">
        <div className="max-w-6xl mx-auto grid md:grid-cols-2 gap-24">
          
          {/* Info */}
          <div>
            <h2 className="text-[#B8975A] text-3xl font-serif tracking-[0.3em] mb-12 uppercase">Contact Us</h2>
            <div className="space-y-8 text-xs tracking-[0.2em] uppercase leading-relaxed text-white/60">
              <div>
                <p className="text-white font-bold mb-2">MY GARAGE A/S</p>
                <p>Wittrupvej 1, 7120 Vejle Øst</p>
              </div>
              <div>
                <p>T +45 70 20 20 20</p>
                <p>M info@mygarage.dk</p>
                <p>W www.mygarage.dk</p>
              </div>
            </div>
          </div>

          {/* Form */}
          <form className="space-y-10">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-10">
              <input type="text" placeholder="NAME" className="bg-transparent border-b border-white/20 py-2 outline-none focus:border-[#B8975A] text-[11px] tracking-widest w-full transition" />
              <input type="email" placeholder="EMAIL" className="bg-transparent border-b border-white/20 py-2 outline-none focus:border-[#B8975A] text-[11px] tracking-widest w-full transition" />
            </div>
            <textarea placeholder="MESSAGE" rows="4" className="bg-transparent border-b border-white/20 py-2 outline-none focus:border-[#B8975A] w-full text-[11px] tracking-widest transition"></textarea>
            
            <div className="flex items-center gap-4">
               <input type="checkbox" id="check" className="accent-[#B8975A]" />
               <label htmlFor="check" className="text-[9px] uppercase tracking-widest text-white/40">I accept that MY GARAGE stores my data for communication.</label>
            </div>

            <button className="border border-white/20 px-12 py-4 text-[10px] tracking-[0.5em] uppercase hover:bg-white hover:text-[#1A2C1C] transition duration-500 block w-full md:w-max">
              Send Message
            </button>
          </form>

        </div>
      </section>

      {/* 7. FOOTER SECTION */}
      <footer className="bg-[#1A2C1C] text-white/30 py-20 px-6 md:px-20 border-t border-white/5">
        <div className="max-w-6xl mx-auto flex flex-col md:flex-row justify-between items-start md:items-center">
          <div className="mb-12 md:mb-0">
            <h3 className="text-[#B8975A] text-2xl font-serif tracking-[0.5em] mb-4">MY GARAGE</h3>
            <p className="text-[10px] tracking-widest">STAY UPDATED</p>
            <div className="flex gap-6 mt-4 text-white/60">
               <span className="hover:text-white cursor-pointer transition">Facebook</span>
               <span className="hover:text-white cursor-pointer transition">Instagram</span>
               <span className="hover:text-white cursor-pointer transition">LinkedIn</span>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-16 text-[10px] tracking-[0.2em] uppercase leading-loose">
            <div>
              <h4 className="text-white mb-4 tracking-[0.3em]">Opening Hours</h4>
              <p>Mon - Fri: 09:00 - 17:30</p>
              <p>Saturday: By Appointment</p>
              <p>Sunday: Closed</p>
            </div>
            <div>
              <h4 className="text-white mb-4 tracking-[0.3em]">Information</h4>
              <p className="hover:text-white cursor-pointer transition">Privacy Policy</p>
              <p className="hover:text-white cursor-pointer transition">Terms of Trade</p>
              <p className="hover:text-white cursor-pointer transition">Cookie Policy</p>
            </div>
          </div>
        </div>
        
        <div className="max-w-6xl mx-auto mt-20 pt-10 border-t border-white/5 flex justify-between items-center text-[9px] tracking-widest">
           <span>© 2024 MY GARAGE - ALL RIGHTS RESERVED</span>
           <div className="flex gap-4 opacity-40 grayscale">
              <img src="https://upload.wikimedia.org/wikipedia/commons/5/5e/Visa_Inc._logo.svg" className="h-3" alt="Visa" />
              <img src="https://upload.wikimedia.org/wikipedia/commons/2/2a/Mastercard-logo.svg" className="h-4" alt="Mastercard" />
           </div>
        </div>
      </footer>

    </div>
  );
};

export default App;