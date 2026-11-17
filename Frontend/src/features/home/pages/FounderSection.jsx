import { mediaUrl } from '@shared/brand/media';

const content = {
  heading: 'Khởi nguồn từ đam mê. Vận hành bằng sự chính xác. Hoàn thiện nhờ chuyên môn.',
  paragraphs: [
    'Từ khi còn nhỏ, ô tô đã là một phần quan trọng trong cuộc sống của tôi. Tôi yêu thích cơ khí, sự độc đáo, thiết kế và sức mạnh đầy mê hoặc của những động cơ lớn. Tại điền trang Rohden gần Stouby, gia đình tôi đã có garage riêng và tổ chức những buổi gặp gỡ dành cho người yêu xe suốt nhiều năm. Chính tại đây, ý tưởng về MY GARAGE đã ra đời.',
    'Tôi muốn tạo nên một điểm hẹn cho những người cùng đam mê, từ chủ xe, chuyên gia mô tô, kỹ thuật viên, người bán xe đến bất kỳ ai yêu thích thế giới động cơ. Khi ấy, tôi thường xuyên di chuyển trên những con đường vùng Jutland giữa điền trang Rohden, Vejle và Horsens. Tôi nhiều lần đi ngang nhà máy kéo sợi cũ của Egetæpper, nơi tôi đã ngưỡng mộ từ thời thơ ấu với những mái vòm bê tông mang dáng vẻ biểu tượng.',
    'Mùa hè năm 2015, tôi tiếp quản các tòa nhà và cùng Giám đốc Lars Bjørnbak bắt đầu hành trình MY GARAGE. Phần còn lại đã trở thành lịch sử.',
  ],
  founder: 'Anders Kirk Johansen',
  role: 'Chủ tịch & Nhà sáng lập',
  signature: 'Chữ ký Anders Kirk Johansen',
};
const videos = [
  { label: 'Chính xác', url: 'https://mygarage.skaevt.com/wp-content/uploads/2025/12/Porsche-andre-biler-MY-GARAGE-Full-HD-1-2.mp4' },
  { label: 'Đam mê', url: 'https://mygarage.skaevt.com/wp-content/uploads/2022/11/MY_GARAGE_Trackday_2022_1920x1080_5MBPS_website_V1.mp4' },
  { label: 'Chuyên môn', url: 'https://mygarage.skaevt.com/wp-content/uploads/2025/10/Video-til-partnerside-afkortet.mp4' },
];

export default function FounderSection() {
  return <section className="home-founder" aria-labelledby="home-founder-heading">
    <div className="home-founder-story">
      <h2 id="home-founder-heading">{content.heading}</h2>
      <div className="home-founder-copy">
        {content.paragraphs.map(paragraph => <p key={paragraph}><em>{paragraph}</em></p>)}
        <p className="home-founder-credit">{content.founder}<br />{content.role}</p>
        <img className="home-founder-signature" src={mediaUrl('https://mygarage.skaevt.com/wp-content/uploads/2020/02/signature@2x-300x62.png')} alt={content.signature} loading="lazy" />
      </div>
    </div>
    <div className="home-founder-videos">{videos.map(video => <figure key={video.url}>
      <video src={mediaUrl(video.url)} autoPlay loop muted playsInline preload="metadata" aria-label={video.label} />
      <figcaption>{video.label}</figcaption>
    </figure>)}</div>
  </section>;
}
