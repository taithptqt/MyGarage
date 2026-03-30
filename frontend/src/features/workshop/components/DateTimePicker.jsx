import React, { useEffect, useState } from "react";
import Calendar from "react-calendar";
import "react-calendar/dist/Calendar.css";
import {
  Box,
  Button,
  Flex,
  Icon,
  Popover,
  PopoverBody,
  PopoverContent,
  PopoverTrigger,
  Text,
  VStack,
} from "@chakra-ui/react";
import { MdClose, MdEvent, MdToday } from "react-icons/md";

const DateTimePicker = ({ value, onChange, placeholder = "Chọn ngày và giờ", minDate }) => {
  const [selectedDate, setSelectedDate] = useState(null);
  const [selectedHour, setSelectedHour] = useState("08");
  const [selectedMinute, setSelectedMinute] = useState("00");
  const [isOpen, setIsOpen] = useState(false);

  useEffect(() => {
    if (!value) {
      setSelectedDate(null);
      setSelectedHour("08");
      setSelectedMinute("00");
      return;
    }

    const d = new Date(value);
    if (!Number.isNaN(d.getTime())) {
      setSelectedDate(d);
      setSelectedHour(String(d.getHours()).padStart(2, "0"));
      setSelectedMinute(String(d.getMinutes()).padStart(2, "0"));
    }
  }, [value]);

  const hours = Array.from({ length: 13 }, (_, i) => String(i + 8).padStart(2, "0"));
  const minutes = Array.from({ length: 60 }, (_, i) => String(i).padStart(2, "0"));

  const updateValue = (date, h, m) => {
    const next = new Date(date);
    next.setHours(Number(h), Number(m), 0, 0);

    const pad = (n) => String(n).padStart(2, "0");
    const formatted = `${next.getFullYear()}-${pad(next.getMonth() + 1)}-${pad(next.getDate())}T${pad(next.getHours())}:${pad(next.getMinutes())}:00`;
    onChange(formatted);
  };

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

  const formatDisplay = () => {
    if (!selectedDate) return "";
    const day = String(selectedDate.getDate()).padStart(2, "0");
    const month = String(selectedDate.getMonth() + 1).padStart(2, "0");
    const year = selectedDate.getFullYear();
    return `${day}/${month}/${year} ${selectedHour}:${selectedMinute}`;
  };

  return (
    <Popover
      isOpen={isOpen}
      onOpen={() => setIsOpen(true)}
      onClose={() => setIsOpen(false)}
      placement="bottom-start"
      closeOnBlur
      strategy="fixed"
      gutter={8}
    >
      <PopoverTrigger>
        <Box>
          <Flex
            cursor="pointer"
            onClick={() => setIsOpen(true)}
            maxW="360px"
            w="100%"
            minH="40px"
            px="12px"
            border="1px solid #2e4e70"
            borderRadius="12px"
            bg="#071d36"
            align="center"
            justify="space-between"
            gap="8px"
          >
            <Text
              flex="1"
              fontSize="sm"
              fontWeight="600"
              color={selectedDate ? "#eaf4ff" : "#aac0d8"}
              whiteSpace="nowrap"
              overflow="hidden"
              textOverflow="ellipsis"
            >
              {formatDisplay() || placeholder}
            </Text>
            <Flex
              w="28px"
              h="28px"
              align="center"
              justify="center"
              borderRadius="8px"
              color="#f5a623"
              fontSize="20px"
              flexShrink={0}
            >
              <Icon as={MdEvent} />
            </Flex>
          </Flex>
        </Box>
      </PopoverTrigger>

      <PopoverContent
        w="auto"
        maxW="640px"
        borderRadius="16px"
        border="1px solid #2e4e70"
        bg="#0a2342"
        color="#eaf4ff"
        boxShadow="0 24px 48px rgba(0, 0, 0, 0.45)"
        zIndex={20000}
      >
        <PopoverBody p="0">
          <Flex direction={{ base: "column", md: "row" }} overflow="hidden" borderRadius="16px">
            <Box p="10px" borderRight="1px solid #2e4e70">
              <Calendar
                onChange={handleDateChange}
                value={selectedDate || new Date()}
                minDate={minDate || new Date()}
                locale="vi-VN"
                className="custom-react-calendar"
              />
              <Flex justify="space-between" mt="10px" px="5px">
                <Button
                  variant="ghost"
                  size="sm"
                  leftIcon={<Icon as={MdToday} />}
                  onClick={() => handleDateChange(new Date())}
                  color="#f5a623"
                >
                  Hôm nay
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  leftIcon={<Icon as={MdClose} />}
                  onClick={() => {
                    setSelectedDate(null);
                    onChange("");
                    setIsOpen(false);
                  }}
                  color="#a9bfd9"
                >
                  Xóa
                </Button>
              </Flex>
            </Box>

            <Flex direction="row" bg="#071d36" p="14px" gap="14px">
              <VStack align="center" spacing="2px">
                <Text fontSize="xs" fontWeight="700" color="#7fa1c5" mb="5px">GIỜ</Text>
                <Box h="200px" overflowY="auto" className="hide-scrollbar" w="56px">
                  {hours.map((h) => (
                    <Button
                      key={h}
                      size="sm"
                      w="100%"
                      bg={selectedHour === h ? "#24cde5" : "transparent"}
                      color={selectedHour === h ? "#03203d" : "#d8e9fb"}
                      _hover={{ bg: selectedHour === h ? "#24cde5" : "rgba(255,255,255,0.08)" }}
                      onClick={() => handleHourSelect(h)}
                      borderRadius="8px"
                      mb="2px"
                    >
                      {h}
                    </Button>
                  ))}
                </Box>
              </VStack>

              <VStack align="center" spacing="2px">
                <Text fontSize="xs" fontWeight="700" color="#7fa1c5" mb="5px">PHÚT</Text>
                <Box h="200px" overflowY="auto" className="hide-scrollbar" w="56px">
                  {minutes.map((m) => (
                    <Button
                      key={m}
                      size="sm"
                      w="100%"
                      bg={selectedMinute === m ? "#24cde5" : "transparent"}
                      color={selectedMinute === m ? "#03203d" : "#d8e9fb"}
                      _hover={{ bg: selectedMinute === m ? "#24cde5" : "rgba(255,255,255,0.08)" }}
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

      <style>{`
        .custom-react-calendar {
          border: none !important;
          width: 250px !important;
          background: #0a2342 !important;
          color: #eaf4ff !important;
        }

        .custom-react-calendar .react-calendar__navigation button {
          color: #eaf4ff !important;
        }

        .custom-react-calendar .react-calendar__month-view__weekdays__weekday {
          color: #9eb8d3 !important;
          text-transform: capitalize;
        }

        .custom-react-calendar .react-calendar__tile {
          color: #dce9f7 !important;
          border-radius: 8px;
        }

        .custom-react-calendar .react-calendar__tile:disabled {
          color: #9bb4cd !important;
          opacity: 0.75 !important;
          background: rgba(255, 255, 255, 0.02) !important;
        }

        .custom-react-calendar .react-calendar__tile:enabled:hover,
        .custom-react-calendar .react-calendar__tile:enabled:focus {
          background: rgba(36, 205, 229, 0.2) !important;
        }

        .custom-react-calendar .react-calendar__tile--active {
          background: #24cde5 !important;
          color: #03203d !important;
        }

        .custom-react-calendar .react-calendar__tile--now {
          background: rgba(245, 166, 35, 0.2) !important;
          color: #ffe3a9 !important;
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
