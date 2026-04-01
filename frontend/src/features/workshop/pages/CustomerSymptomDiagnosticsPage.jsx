import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Box,
  Flex,
  Heading,
  Text,
  VStack,
  HStack,
  SimpleGrid,
  Button,
  Input,
  Textarea,
  Select,
  Icon,
  Tag,
  TagLabel,
  useToast,
  Divider,
  FormLabel,
  FormControl,
} from "@chakra-ui/react";
import {
  Car,
  Wrench,
  AlertTriangle,
  Activity,
  VolumeX,
  Droplets,
  Wind,
  Zap,
  Clock,
  CheckCircle,
  Camera,
  ArrowLeft,
  Info,
} from "lucide-react";

const SYSTEM_CATEGORIES = [
  { id: "engine", label: "??ng co", icon: Activity },
  { id: "transmission", label: "Hộp số", icon: Wrench },
  { id: "brakes", label: "Phanh & ABS", icon: AlertTriangle },
  { id: "steering", label: "Hệ thống Lái / Gầm", icon: Car },
  { id: "electrical", label: "Điện / Bình ắc quy", icon: Zap },
  { id: "hvac", label: "Điều hòa / Sưởi", icon: Wind },
  { id: "fluids", label: "Chất lỏng / Rò rỉ", icon: Droplets },
  { id: "other", label: "Khác", icon: Info },
];

const SYMPTOM_TYPES = {
  engine: ["Rung giật", "Mất công suất", "Chết máy", "Không khởi động được", "Khói bất thường"],
  transmission: ["Khó sang số", "Trượt số", "Giật cục", "Chậm phản hồi"],
  brakes: ["Kêu rít / Xào xạo", "Chân phanh mềm", "Xe bị lệch khi phanh", "Cứng chân phanh"],
  steering: ["Vô lăng nặng", "Xe nhao lái", "Lốp mòn không đều", "Tiếng va dập gầm"],
  electrical: ["Đèn pha mờ", "Còi tịt", "Đồng hồ taplo lỗi", "Hết bình"],
  hvac: ["Không mát", "Mát yếu", "Mùi hôi", "Quạt gió kêu"],
  fluids: ["Nước làm mát", "Dầu máy", "Dầu phanh", "Nước rửa kính"],
  other: ["Tiếng ồn lạ", "Mùi khét", "Hư hỏng ngoại thất", "Khác"],
};

export default function CustomerSymptomDiagnosticsPage() {
  const navigate = useNavigate();
  const toast = useToast();

  const [form, setForm] = useState({
    vehicleInfo: "",
    mileage: "",
    dashboardCodes: "",
    primarySystem: "",
    symptoms: [],
    frequency: "",
    occurrenceCondition: "",
    timeline: "",
    customerDescription: "",
  });

  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSystemSelect = (sysId) => {
    setForm({ ...form, primarySystem: sysId, symptoms: [] });
  };

  const toggleSymptom = (symptom) => {
    setForm((prev) => {
      const isSelected = prev.symptoms.includes(symptom);
      if (isSelected) {
        return { ...prev, symptoms: prev.symptoms.filter((s) => s !== symptom) };
      }
      return { ...prev, symptoms: [...prev.symptoms, symptom] };
    });
  };

  const handleSubmit = async () => {
    setIsSubmitting(true);
    // Giả lập lưu dữ liệu và call API
    setTimeout(() => {
      setIsSubmitting(false);
      toast({
        title: "Đã lưu chuẩn đoán",
        description: "Dữ liệu triệu chứng đã được ghi nhận chờ phân tích.",
        status: "success",
        duration: 3000,
        isClosable: true,
        position: "top-right",
      });
      // useNavigate back or clear form
    }, 1500);
  };

  return (
    <Box className="workshop-dashboard" p={6}>
      <Flex maxW="1200px" mx="auto" direction="column" gap={6}>
        {/* Header Section */}
        <Flex justify="space-between" align="center" bg="white" p={5} borderRadius="xl" boxShadow="sm" border="1px solid #e2e8f0">
          <HStack spacing={4}>
            <Button variant="ghost" onClick={() => navigate("/workshop/reception")}>
              <Icon as={ArrowLeft} mr={2} /> Quay lại
            </Button>
            <Box>
              <Heading size="md" color="#2b3674">Chi tiết Triệu chứng (Dành cho AI)</Heading>
              <Text color="#a3aed0" fontSize="sm">Ghi nhận thông tin chuẩn đoán từ mô tả của khách hàng</Text>
            </Box>
          </HStack>
          <Button colorScheme="blue" leftIcon={<Icon as={CheckCircle} />} onClick={handleSubmit} isLoading={isSubmitting}>
            Hoàn tất Ghi nhận
          </Button>
        </Flex>

        <SimpleGrid columns={{ base: 1, lg: 3 }} spacing={6}>
          {/* Left Column - General Info */}
          <VStack spacing={6} align="stretch" gridColumn={{ lg: "span 1" }}>
            <Box bg="white" p={5} borderRadius="xl" boxShadow="sm" border="1px solid #e2e8f0">
              <HStack mb={4} color="#4318ff">
                <Icon as={Car} />
                <Heading size="sm">Thông tin đầu vào</Heading>
              </HStack>
              
              <VStack spacing={4} align="stretch">
                <FormControl>
                  <FormLabel fontSize="sm" color="#2b3674" fontWeight="600">Biển số / Lệnh tham chiếu *</FormLabel>
                  <Input 
                    placeholder="VD: 51H-123.45 hoặc ORD-001" 
                    value={form.vehicleInfo}
                    onChange={(e) => setForm({...form, vehicleInfo: e.target.value})}
                    focusBorderColor="#4318ff"
                  />
                </FormControl>

                <FormControl>
                  <FormLabel fontSize="sm" color="#2b3674" fontWeight="600">Số ODO hiện tại (km)</FormLabel>
                  <Input 
                    type="number"
                    placeholder="VD: 45000" 
                    value={form.mileage}
                    onChange={(e) => setForm({...form, mileage: e.target.value})}
                    focusBorderColor="#4318ff"
                  />
                </FormControl>

                <FormControl>
                  <FormLabel fontSize="sm" color="#2b3674" fontWeight="600">Mã lỗi OBD / Đèn cảnh báo</FormLabel>
                  <Input 
                    placeholder="VD: Đèn cá vàng sáng, mã P0300..." 
                    value={form.dashboardCodes}
                    onChange={(e) => setForm({...form, dashboardCodes: e.target.value})}
                    focusBorderColor="#4318ff"
                  />
                </FormControl>
              </VStack>
            </Box>

            <Box bg="white" p={5} borderRadius="xl" boxShadow="sm" border="1px solid #e2e8f0">
              <HStack mb={4} color="#f56565">
                <Icon as={Clock} />
                <Heading size="sm">Tình trạng xuất hiện</Heading>
              </HStack>
              
              <VStack spacing={4} align="stretch">
                <FormControl>
                  <FormLabel fontSize="sm" color="#2b3674" fontWeight="600">Thời gian bắt đầu</FormLabel>
                  <Select 
                    placeholder="Chọn khung thời gian" 
                    focusBorderColor="#4318ff"
                    value={form.timeline}
                    onChange={(e) => setForm({...form, timeline: e.target.value})}
                  >
                    <option value="today">Vừa mới xảy ra (Hôm nay)</option>
                    <option value="few_days">Vài ngày trước</option>
                    <option value="weeks">Vài tuần trước</option>
                    <option value="months">Đã bị từ lâu (&gt; 1 tháng)</option>
                  </Select>
                </FormControl>

                <FormControl>
                  <FormLabel fontSize="sm" color="#2b3674" fontWeight="600">Tần suất</FormLabel>
                  <Select 
                    placeholder="Tần suất xảy ra" 
                    focusBorderColor="#4318ff"
                    value={form.frequency}
                    onChange={(e) => setForm({...form, frequency: e.target.value})}
                  >
                    <option value="constant">Liên tục (Lúc nào cũng bị)</option>
                    <option value="intermittent">Thỉnh thoảng (Lúc bị lúc không)</option>
                    <option value="rare">Rất hiếm khi</option>
                  </Select>
                </FormControl>

                <FormControl>
                  <FormLabel fontSize="sm" color="#2b3674" fontWeight="600">Điều kiện xảy ra rõ nhất</FormLabel>
                  <Select 
                    placeholder="Hoàn cảnh phát sinh lỗi" 
                    focusBorderColor="#4318ff"
                    value={form.occurrenceCondition}
                    onChange={(e) => setForm({...form, occurrenceCondition: e.target.value})}
                  >
                    <option value="cold_start">Khi nổ máy nguội (Sáng sớm)</option>
                    <option value="idling">Khi nổ máy không tải (Dừng đèn đỏ)</option>
                    <option value="accelerating">Khi tăng tốc / đạp ga</option>
                    <option value="braking">Khi r? phanh</option>
                    <option value="turning">Khi đánh lái / Cua</option>
                    <option value="high_speed">Khi chạy tốc độ cao (&gt; 60km/h)</option>
                    <option value="always">Bất kỳ lúc nào</option>
                  </Select>
                </FormControl>
              </VStack>
            </Box>
          </VStack>

          {/* Right Column - Symptoms Details */}
          <VStack spacing={6} align="stretch" gridColumn={{ lg: "span 2" }}>
            <Box bg="white" p={5} borderRadius="xl" boxShadow="sm" border="1px solid #e2e8f0">
               <HStack mb={4} color="#ffb547">
                <Icon as={AlertTriangle} />
                <Heading size="sm">Phân loại Triệu chứng *</Heading>
              </HStack>

              <Text fontSize="sm" color="#707eae" mb={3}>1. Chọn vùng hệ thống nghi ngờ lỗi:</Text>
              <SimpleGrid columns={{ base: 2, md: 4 }} spacing={3} mb={6}>
                {SYSTEM_CATEGORIES.map((sys) => (
                  <Button
                    key={sys.id}
                    variant={form.primarySystem === sys.id ? "solid" : "outline"}
                    colorScheme={form.primarySystem === sys.id ? "blue" : "gray"}
                    height="auto"
                    py={3}
                    display="flex"
                    flexDirection="column"
                    gap={2}
                    onClick={() => handleSystemSelect(sys.id)}
                    borderColor={form.primarySystem === sys.id ? "#4318ff" : "#e2e8f0"}
                    bg={form.primarySystem === sys.id ? "#4318ff" : "white"}
                    _hover={{ bg: form.primarySystem === sys.id ? "#3311db" : "#f4f7fe" }}
                  >
                    <Icon as={sys.icon} boxSize={5} color={form.primarySystem === sys.id ? "white" : "#4318ff"} />
                    <Text fontSize="xs" fontWeight={form.primarySystem === sys.id ? "bold" : "normal"} whiteSpace="normal" noOfLines={2}>
                      {sys.label}
                    </Text>
                  </Button>
                ))}
              </SimpleGrid>

              {form.primarySystem && (
                <>
                  <Divider my={4} borderColor="#e2e8f0" />
                  <Text fontSize="sm" color="#707eae" mb={3}>2. Chọn các triệu chứng cụ thể (có thể chọn nhiều):</Text>
                  <Flex wrap="wrap" gap={2}>
                    {SYMPTOM_TYPES[form.primarySystem]?.map((symp, idx) => {
                      const isSelected = form.symptoms.includes(symp);
                      return (
                        <Tag
                          key={idx}
                          size="lg"
                          variant={isSelected ? "solid" : "outline"}
                          colorScheme={isSelected ? "cyan" : "gray"}
                          cursor="pointer"
                          onClick={() => toggleSymptom(symp)}
                          borderRadius="full"
                          border={isSelected ? "none" : "1px solid #e2e8f0"}
                          px={4}
                          py={2}
                          bg={isSelected ? "#00b5d8" : "white"}
                          color={isSelected ? "white" : "#2b3674"}
                          _hover={{ bg: isSelected ? "#0093b0" : "#f4f7fe" }}
                          transition="all 0.2s"
                        >
                          {isSelected && <TagLeftIcon boxSize="12px" as={CheckCircle} />}
                          <TagLabel fontSize="sm" fontWeight={isSelected ? "600" : "500"}>{symp}</TagLabel>
                        </Tag>
                      );
                    })}
                  </Flex>
                </>
              )}
            </Box>

            <Box bg="white" p={5} borderRadius="xl" boxShadow="sm" border="1px solid #e2e8f0">
              <HStack justify="space-between" mb={4}>
                <HStack color="#05cd99">
                  <Icon as={VolumeX} />
                  <Heading size="sm">Mô tả chi tiết của khách hàng</Heading>
                </HStack>
              </HStack>
              
              <FormControl>
                 <Textarea 
                    placeholder="VD: 'Sáng nay tôi đề máy thì nghe tiếng rít rít rồi lên đèn check engine, xe rung bần bật không chạy được...'" 
                    rows={4}
                    value={form.customerDescription}
                    onChange={(e) => setForm({...form, customerDescription: e.target.value})}
                    focusBorderColor="#05cd99"
                    borderRadius="lg"
                  />
                  <Text fontSize="xs" color="#a3aed0" mt={2}>Trích dẫn nguyên văn lời khách hàng để mô hình AI nắm bắt bối cảnh tốt nhất.</Text>
              </FormControl>
            </Box>

            {/* Media placeholder */}
            <Box bg="#f8fafc" p={5} borderRadius="xl" border="1px dashed #cbd5e1" textAlign="center">
              <VStack spacing={3}>
                <Icon as={Camera} boxSize={8} color="#94a3b8" />
                <Heading size="sm" color="#64748b">Đính kèm Hình ảnh / Video / Audio (Sắp tới)</Heading>
                <Text fontSize="sm" color="#94a3b8" maxW="400px">
                  Khách hàng gửi clip nghe tiếng máy gõ, hình ảnh vết nứt rò rỉ dưới gầm có thể đính kèm ở đây để cung cấp bằng chứng cho AI.
                </Text>
                <Button size="sm" colorScheme="gray" isDisabled>
                  Tải tệp lên
                </Button>
              </VStack>
            </Box>
          </VStack>
        </SimpleGrid>
      </Flex>
    </Box>
  );
}


