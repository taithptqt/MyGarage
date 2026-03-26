import React, { useState, useEffect, useRef } from "react";
import Calendar from "react-calendar";
import "react-calendar/dist/Calendar.css";
import {
  Box,
  Button,
  Flex,
  Grid,
  Icon,
  Input,
  InputGroup,
  InputRightElement,
  Popover,
  PopoverTrigger,
  PopoverContent,
  PopoverBody,
  Text,
  VStack,
  useColorModeValue,
  Portal,
} from "@chakra-ui/react";
import { MdEvent, MdAccessTime, MdToday, MdClose } from "react-icons/md";

/**
 * Custom DateTimePicker for MyGarage
 * Constraints:
 * - Hours: 08:00 - 20:00
 * - Minutes: 00 - 59
 */
const DateTimePicker = ({ value, onChange, placeholder = "Chọn ngày và giờ", minDate }) => {
  const [selectedDate, setSelectedDate] = useState(null);
  const [selectedHour, setSelectedHour] = useState("08");
  const [selectedMinute, setSelectedMinute] = useState("00");
  const [isOpen, setIsOpen] = useState(false);

  const textColor = useColorModeValue("secondaryGray.900", "white");
  const brandColor = "#4318ff";
  const hoverBg = useColorModeValue("secondaryGray.200", "whiteAlpha.100");

  useEffect(() => {
    if (value) {
      const d = new Date(value);
      if (!isNaN(d.getTime())) {
        setSelectedDate(d);
        setSelectedHour(d.getHours().toString().padStart(2, "0"));
        setSelectedMinute(d.getMinutes().toString().padStart(2, "0"));
      }
    }
  }, [value]);

  const handleDateChange = (date) => {
    setSelectedDate(date);
    updateValue(date, selectedHour, selectedMinute);
  };

  const handleHourSelect = (h) => {
    setSelectedHour(h);
    if (selectedDate) updateValue(selectedDate, h, selectedMinute);
  };

  const handleMinuteSelect = (m) => {
    setSelectedMinute(m);
    if (selectedDate) updateValue(selectedDate, selectedHour, m);
  };

  const updateValue = (date, h, m) => {
    const newDate = new Date(date);
    newDate.setHours(parseInt(h), parseInt(m), 0, 0);
    
    // Format to local ISO string without 'Z' to avoid UTC shifts
    const pad = (num) => num.toString().padStart(2, '0');
    const formatted = `${newDate.getFullYear()}-${pad(newDate.getMonth() + 1)}-${pad(newDate.getDate())}T${pad(newDate.getHours())}:${pad(newDate.getMinutes())}:00`;
    
    onChange(formatted);
  };

  const formatDisplay = () => {
    if (!selectedDate) return "";
    const day = selectedDate.getDate().toString().padStart(2, "0");
    const month = (selectedDate.getMonth() + 1).toString().padStart(2, "0");
    const year = selectedDate.getFullYear();
    return `${day}/${month}/${year} ${selectedHour}:${selectedMinute}`;
  };

  const hours = Array.from({ length: 13 }, (_, i) => (i + 8).toString().padStart(2, "0")); // 08 - 20
  const minutes = Array.from({ length: 60 }, (_, i) => i.toString().padStart(2, "0"));

  return (
    <Popover isOpen={isOpen} onClose={() => setIsOpen(false)} placement="bottom-start" closeOnBlur={true}>
      <PopoverTrigger>
        <InputGroup onClick={() => setIsOpen(true)} cursor="pointer">
          <Input
            readOnly
            placeholder={placeholder}
            value={formatDisplay()}
            cursor="pointer"
            fontSize="sm"
            fontWeight="500"
            color={textColor}
            borderRadius="12px"
            border="1px solid"
            borderColor="secondaryGray.300"
            _focus={{ borderColor: "brand.500" }}
          />
          <InputRightElement color="secondaryGray.500">
            <Icon as={MdEvent} />
          </InputRightElement>
        </InputGroup>
      </PopoverTrigger>
      <Portal>
        <PopoverContent 
          w="auto" 
          maxW="600px" 
          boxShadow="14px 17px 40px 4px rgba(112, 144, 176, 0.18)" 
          borderRadius="20px" 
          border="none"
          zIndex="popover"
        >
          <PopoverBody p="0">
            <Flex direction={{ base: "column", md: "row" }} overflow="hidden" borderRadius="20px">
              {/* Date Column */}
              <Box p="10px" borderRight="1px solid" borderColor="secondaryGray.200">
                <Calendar
                  onChange={handleDateChange}
                  value={selectedDate || new Date()}
                  minDate={minDate || new Date()}
                  locale="vi-VN"
                  className="custom-react-calendar"
                />
                <Flex justify="space-between" mt="10px" px="5px">
                  <Button variant="ghost" size="sm" leftIcon={<Icon as={MdToday} />} onClick={() => handleDateChange(new Date())} color={brandColor}>
                    Hôm nay
                  </Button>
                  <Button variant="ghost" size="sm" leftIcon={<Icon as={MdClose} />} onClick={() => { setSelectedDate(null); onChange(""); setIsOpen(false); }}>
                    Xóa
                  </Button>
                </Flex>
              </Box>

              {/* Time Column */}
              <Flex direction="row" bg="secondaryGray.100" p="15px" gap="15px">
                {/* Hours */}
                <VStack align="center" spacing="2px">
                  <Text fontSize="xs" fontWeight="700" color="secondaryGray.600" mb="5px">GIỜ</Text>
                  <Box h="200px" overflowY="auto" className="hide-scrollbar" w="50px">
                    {hours.map(h => (
                      <Button
                        key={h}
                        size="sm"
                        w="100%"
                        variant={selectedHour === h ? "solid" : "ghost"}
                        colorScheme={selectedHour === h ? "brand" : "gray"}
                        bg={selectedHour === h ? brandColor : "transparent"}
                        color={selectedHour === h ? "white" : textColor}
                        onClick={() => handleHourSelect(h)}
                        borderRadius="8px"
                        mb="2px"
                      >
                        {h}
                      </Button>
                    ))}
                  </Box>
                </VStack>

                {/* Minutes */}
                <VStack align="center" spacing="2px">
                  <Text fontSize="xs" fontWeight="700" color="secondaryGray.600" mb="5px">PHÚT</Text>
                  <Box h="200px" overflowY="auto" className="hide-scrollbar" w="50px">
                    {minutes.map(m => (
                      <Button
                        key={m}
                        size="sm"
                        w="100%"
                        variant={selectedMinute === m ? "solid" : "ghost"}
                        colorScheme={selectedMinute === m ? "brand" : "gray"}
                        bg={selectedMinute === m ? brandColor : "transparent"}
                        color={selectedMinute === m ? "white" : textColor}
                        onClick={() => handleMinuteSelect(m)}
                        borderRadius="8px"
                        mb="2px"
                      >
                        {m}
                      </Button>
                    ))}
                  </Box>
                </VStack>
              </Flex>
            </Flex>
          </PopoverBody>
        </PopoverContent>
      </Portal>

      <style>{`
        .custom-react-calendar {
          border: none !important;
          font-family: inherit !important;
          width: 250px !important;
        }
        .custom-react-calendar .react-calendar__tile--active {
          background: ${brandColor} !important;
          border-radius: 8px;
        }
        .custom-react-calendar .react-calendar__tile--now {
          background: #eef2ff !important;
          border-radius: 8px;
          color: ${brandColor} !important;
        }
        .custom-react-calendar .react-calendar__navigation button:enabled:hover,
        .custom-react-calendar .react-calendar__navigation button:enabled:focus {
          background-color: #f4f7fe !important;
          border-radius: 8px;
        }
        .hide-scrollbar::-webkit-scrollbar {
          display: none;
        }
        .hide-scrollbar {
          -ms-overflow-style: none;
          scrollbar-width: none;
        }
      `}</style>
    </Popover>
  );
};

export default DateTimePicker;
