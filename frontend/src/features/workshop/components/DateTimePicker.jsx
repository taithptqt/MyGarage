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
  useDisclosure,
  VStack,
} from "@chakra-ui/react";
import { MdClose, MdEvent, MdToday } from "react-icons/md";

const DateTimePicker = ({
  value,
  onChange,
  placeholder = "Chọn ngày và giờ",
  minDate,
  showTime = true,
}) => {
  const [selectedDate, setSelectedDate] = useState(null);
  const [selectedHour, setSelectedHour] = useState("08");
  const [selectedMinute, setSelectedMinute] = useState("00");
  const { isOpen, onOpen, onClose } = useDisclosure();

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

  const hours = Array.from({ length: 12 }, (_, i) => String(i + 8).padStart(2, "0"));
  const minutes = ["00", "15", "30", "45"];

  const updateValue = (date, h, m) => {
    const next = new Date(date);
    next.setHours(Number(h), Number(m), 0, 0);

    const pad = (n) => String(n).padStart(2, "0");
    const formatted = `${next.getFullYear()}-${pad(next.getMonth() + 1)}-${pad(next.getDate())}T${pad(next.getHours())}:${pad(next.getMinutes())}:00`;
    onChange(formatted);
  };

  const handleDateChange = (date) => {
    setSelectedDate(date);
    if (showTime) {
      updateValue(date, selectedHour, selectedMinute);
      return;
    }
    updateValue(date, "08", "00");
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
    if (!showTime) return `${day}/${month}/${year}`;
    return `${day}/${month}/${year} ${selectedHour}:${selectedMinute}`;
  };

  return (
    <Popover
      isOpen={isOpen}
      onOpen={onOpen}
      onClose={onClose}
      placement="bottom-start"
      closeOnBlur
      gutter={8}
    >
      <PopoverTrigger>
        <Button
          type="button"
          w="100%"
          maxW="360px"
          minH="45px"
          p="0 13px"
          bg="#ffffff"
          border="1px solid rgba(29, 42, 38, 0.2)"
          borderRadius="12px"
          cursor="pointer"
          display="flex"
          alignItems="center"
          justifyContent="space-between"
          transition="all 0.2s"
          _active={{ transform: "scale(0.98)" }}
          _hover={{ borderColor: "rgba(197, 160, 89, 0.42)", bg: "#fcfcfc" }}
          _focus={{
            boxShadow: "0 0 0 3px rgba(197, 160, 89, 0.25)",
            borderColor: "#c5a059",
            outline: "none",
          }}
          _focusVisible={{
            boxShadow: "0 0 0 3px rgba(197, 160, 89, 0.25)",
            borderColor: "#c5a059",
            outline: "none",
          }}
        >
          <Text
            flex="1"
            fontSize="16px"
            fontWeight="600"
            color={selectedDate ? "#1d2a26" : "rgba(29, 42, 38, 0.52)"}
            whiteSpace="nowrap"
            overflow="hidden"
            textOverflow="ellipsis"
            textAlign="left"
          >
            {formatDisplay() || placeholder}
          </Text>
          <Icon as={MdEvent} boxSize="20px" color="#c5a059" />
        </Button>
      </PopoverTrigger>

      <PopoverContent
        w="auto"
        maxW={showTime ? "640px" : "360px"}
        borderRadius="16px"
        border="1px solid rgba(29, 42, 38, 0.2)"
        bg="#ffffff"
        color="#1d2a26"
        boxShadow="0 24px 48px rgba(29, 42, 38, 0.15)"
        zIndex={9999}
        _focus={{ outline: "none" }}
      >
        <PopoverBody p="0">
          <Flex direction={{ base: "column", md: "row" }} overflow="hidden" borderRadius="16px">
            <Box p="10px" borderRight="1px solid rgba(29, 42, 38, 0.1)">
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
                  }}
                  color="#a9bfd9"
                >
                  Xóa
                </Button>
              </Flex>
            </Box>

            {showTime ? (
              <Flex direction="row" bg="#f8fbff" p="14px" gap="14px">
                <VStack align="center" spacing="2px">
                  <Text fontSize="xs" fontWeight="700" color="#7fa1c5" mb="5px">
                    GIỜ
                  </Text>
                  <Box h="200px" overflowY="auto" className="hide-scrollbar" w="56px">
                    {hours.map((h) => (
                      <Button
                        key={h}
                        size="sm"
                        w="100%"
                        bg={selectedHour === h ? "#2f6bd8" : "transparent"}
                        color={selectedHour === h ? "#ffffff" : "#1d2a26"}
                        _hover={{ bg: selectedHour === h ? "#2f6bd8" : "rgba(47,107,216,0.12)" }}
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
                  <Text fontSize="xs" fontWeight="700" color="#7fa1c5" mb="5px">
                    PHÚT
                  </Text>
                  <Box h="200px" overflowY="auto" className="hide-scrollbar" w="56px">
                    {minutes.map((m) => (
                      <Button
                        key={m}
                        size="sm"
                        w="100%"
                        bg={selectedMinute === m ? "#2f6bd8" : "transparent"}
                        color={selectedMinute === m ? "#ffffff" : "#1d2a26"}
                        _hover={{ bg: selectedMinute === m ? "#2f6bd8" : "rgba(47,107,216,0.12)" }}
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
            ) : null}
          </Flex>
        </PopoverBody>
      </PopoverContent>

      <style>{`
        .custom-react-calendar {
          border: none !important;
          width: 250px !important;
          background: #ffffff !important;
          color: #1d2a26 !important;
        }

        .custom-react-calendar .react-calendar__navigation button {
          color: #1d2a26 !important;
        }

        .custom-react-calendar .react-calendar__month-view__weekdays__weekday {
          color: rgba(29, 42, 38, 0.6) !important;
          text-transform: capitalize;
        }

        .custom-react-calendar .react-calendar__tile {
          color: #1d2a26 !important;
          border-radius: 8px;
        }

        .custom-react-calendar .react-calendar__tile:disabled {
          color: rgba(29, 42, 38, 0.3) !important;
          opacity: 0.75 !important;
          background: rgba(0, 0, 0, 0.02) !important;
        }

        .custom-react-calendar .react-calendar__tile:enabled:hover,
        .custom-react-calendar .react-calendar__tile:enabled:focus {
          background: rgba(47, 107, 216, 0.15) !important;
        }

        .custom-react-calendar .react-calendar__tile--active {
          background: #2f6bd8 !important;
          color: #ffffff !important;
        }

        .custom-react-calendar .react-calendar__tile--now {
          background: rgba(138, 106, 46, 0.1) !important;
          color: #8a6a2e !important;
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
