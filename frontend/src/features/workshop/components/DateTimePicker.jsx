import React, { useEffect, useMemo, useState } from "react";
import Calendar from "react-calendar";
import "react-calendar/dist/Calendar.css";
import {
  Box,
  Button,
  Flex,
  Icon,
  Portal,
  Popover,
  PopoverBody,
  PopoverContent,
  PopoverTrigger,
  Text,
  useDisclosure,
  VStack
} from "@chakra-ui/react";
import { MdClose, MdEvent, MdToday } from "react-icons/md";

function padTime(value) {
  return String(value || "").padStart(2, "0");
}

function normalizeTimeMap(mapInput) {
  if (!mapInput || typeof mapInput !== "object") return null;
  const entries = Object.entries(mapInput)
    .map(([hour, minuteList]) => {
      const normalizedMinutes = Array.from(
        new Set(
          (Array.isArray(minuteList) ? minuteList : [])
            .map((minute) => padTime(minute))
            .filter((minute) => /^\d{2}$/.test(minute))
        )
      ).sort((a, b) => Number(a) - Number(b));
      return [padTime(hour), normalizedMinutes];
    })
    .filter(([, minuteList]) => minuteList.length > 0)
    .sort((a, b) => Number(a[0]) - Number(b[0]));

  return Object.fromEntries(entries);
}

const DateTimePicker = ({
  value,
  onChange,
  placeholder = "Chọn ngày và giờ",
  minDate,
  maxDate,
  showTime = true,
  availableTimeMap = null,
  loadingTimeOptions = false,
  noTimeOptionsText = "Không có khung giờ khả dụng cho ngày đã chọn.",
  autoSelectNowOnOpen = false,
  onOpenRequest = null
}) => {
  const [selectedDate, setSelectedDate] = useState(null);
  const [selectedHour, setSelectedHour] = useState("08");
  const [selectedMinute, setSelectedMinute] = useState("00");
  const { isOpen, onOpen, onClose } = useDisclosure();

  const externalTimeMap = useMemo(
    () => (showTime ? normalizeTimeMap(availableTimeMap) : null),
    [availableTimeMap, showTime]
  );
  const hasExternalTimeMap = externalTimeMap !== null;

  const hours = useMemo(() => {
    if (!showTime) return [];
    if (hasExternalTimeMap) return Object.keys(externalTimeMap || {});
    return Array.from({ length: 12 }, (_, i) => String(i + 8).padStart(2, "0"));
  }, [externalTimeMap, hasExternalTimeMap, showTime]);

  const minutes = useMemo(() => {
    if (!showTime) return [];
    if (hasExternalTimeMap) {
      if (hours.length === 0) return [];
      const hour = hours.includes(selectedHour) ? selectedHour : hours[0];
      return externalTimeMap?.[hour] || [];
    }
    return ["00", "15", "30", "45"];
  }, [externalTimeMap, hasExternalTimeMap, hours, selectedHour, showTime]);

  const updateValue = (date, hour, minute) => {
    if (!date || !hour || !minute) return;
    const next = new Date(date);
    next.setHours(Number(hour), Number(minute), 0, 0);

    const formatted = `${next.getFullYear()}-${padTime(next.getMonth() + 1)}-${padTime(next.getDate())}T${padTime(
      next.getHours()
    )}:${padTime(next.getMinutes())}:00`;
    onChange(formatted);
  };

  useEffect(() => {
    if (!value) {
      setSelectedDate(null);
      setSelectedHour("08");
      setSelectedMinute("00");
      return;
    }

    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) return;
    setSelectedDate(parsed);
    setSelectedHour(padTime(parsed.getHours()));
    setSelectedMinute(padTime(parsed.getMinutes()));
  }, [value]);

  useEffect(() => {
    if (!showTime || !hasExternalTimeMap || loadingTimeOptions) return;
    if (hours.length === 0) return;

    const nextHour = hours.includes(selectedHour) ? selectedHour : hours[0];
    const minuteOptions = externalTimeMap?.[nextHour] || [];
    const nextMinute = minuteOptions.includes(selectedMinute) ? selectedMinute : minuteOptions[0];

    if (nextHour !== selectedHour) setSelectedHour(nextHour);
    if (nextMinute && nextMinute !== selectedMinute) setSelectedMinute(nextMinute);

    if (selectedDate && nextMinute && (nextHour !== selectedHour || nextMinute !== selectedMinute)) {
      updateValue(selectedDate, nextHour, nextMinute);
    }
  }, [
    externalTimeMap,
    hasExternalTimeMap,
    hours,
    loadingTimeOptions,
    selectedDate,
    selectedHour,
    selectedMinute,
    showTime
  ]);

  useEffect(() => {
    if (!isOpen || !autoSelectNowOnOpen || selectedDate || value) return;

    const now = new Date();
    const nowHour = padTime(now.getHours());
    const nowMinute = padTime(now.getMinutes());
    setSelectedDate(now);

    if (!showTime) {
      updateValue(now, "08", "00");
      return;
    }

    if (hasExternalTimeMap) {
      const hourKeys = Object.keys(externalTimeMap || {});
      if (hourKeys.length === 0) {
        onChange("");
        return;
      }

      let nextHour = hourKeys.includes(nowHour) ? nowHour : hourKeys[0];
      let minuteOptions = externalTimeMap?.[nextHour] || [];
      let nextMinute = minuteOptions.includes(nowMinute) ? nowMinute : minuteOptions[0];

      if (!nextMinute) {
        let closestDiff = Number.POSITIVE_INFINITY;
        hourKeys.forEach((hour) => {
          const options = externalTimeMap?.[hour] || [];
          options.forEach((minute) => {
            const total = Number(hour) * 60 + Number(minute);
            const diff = Math.abs(total - (now.getHours() * 60 + now.getMinutes()));
            if (diff < closestDiff) {
              closestDiff = diff;
              nextHour = hour;
              nextMinute = minute;
            }
          });
        });
      }

      if (!nextMinute) {
        onChange("");
        return;
      }

      setSelectedHour(nextHour);
      setSelectedMinute(nextMinute);
      updateValue(now, nextHour, nextMinute);
      return;
    }

    setSelectedHour(nowHour);
    setSelectedMinute(nowMinute);
    updateValue(now, nowHour, nowMinute);
  }, [autoSelectNowOnOpen, externalTimeMap, hasExternalTimeMap, isOpen, onChange, selectedDate, showTime, value]);

  const handleDateChange = (date) => {
    setSelectedDate(date);
    if (!showTime) {
      updateValue(date, "08", "00");
      onClose();
      return;
    }

    if (loadingTimeOptions) return;

    if (hasExternalTimeMap) {
      if (hours.length === 0) {
        onChange("");
        return;
      }
      const nextHour = hours.includes(selectedHour) ? selectedHour : hours[0];
      const minuteOptions = externalTimeMap?.[nextHour] || [];
      const nextMinute = minuteOptions.includes(selectedMinute) ? selectedMinute : minuteOptions[0];
      if (!nextMinute) {
        onChange("");
        return;
      }
      setSelectedHour(nextHour);
      setSelectedMinute(nextMinute);
      updateValue(date, nextHour, nextMinute);
      return;
    }

    updateValue(date, selectedHour, selectedMinute);
  };

  const handleHourSelect = (hour) => {
    if (hasExternalTimeMap) {
      const minuteOptions = externalTimeMap?.[hour] || [];
      const nextMinute = minuteOptions.includes(selectedMinute) ? selectedMinute : minuteOptions[0];
      if (!nextMinute) return;
      setSelectedHour(hour);
      setSelectedMinute(nextMinute);
      if (selectedDate) updateValue(selectedDate, hour, nextMinute);
      return;
    }

    setSelectedHour(hour);
    if (selectedDate) updateValue(selectedDate, hour, selectedMinute);
  };

  const handleMinuteSelect = (minute) => {
    if (hasExternalTimeMap) {
      const minuteOptions = externalTimeMap?.[selectedHour] || [];
      if (!minuteOptions.includes(minute)) return;
    }
    setSelectedMinute(minute);
    if (selectedDate) updateValue(selectedDate, selectedHour, minute);
  };

  const formatDisplay = () => {
    if (!selectedDate) return "";
    const day = padTime(selectedDate.getDate());
    const month = padTime(selectedDate.getMonth() + 1);
    const year = selectedDate.getFullYear();
    if (!showTime) return `${day}/${month}/${year}`;
    return `${day}/${month}/${year} ${selectedHour}:${selectedMinute}`;
  };

  const hasNoTimeOptions = showTime && hasExternalTimeMap && hours.length === 0 && !loadingTimeOptions;
  const handlePopoverOpen = () => {
    if (typeof onOpenRequest === "function") {
      const shouldOpen = onOpenRequest();
      if (shouldOpen === false) return;
    }
    onOpen();
  };

  return (
    <Popover
      isOpen={isOpen}
      onOpen={handlePopoverOpen}
      onClose={onClose}
      placement="bottom-start"
      closeOnBlur
      strategy="fixed"
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
            outline: "none"
          }}
          _focusVisible={{
            boxShadow: "0 0 0 3px rgba(197, 160, 89, 0.25)",
            borderColor: "#c5a059",
            outline: "none"
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

      <Portal>
        <PopoverContent
          rootProps={{ zIndex: 26000 }}
          w="auto"
          maxW={showTime ? "640px" : "360px"}
          borderRadius="16px"
          border="1px solid rgba(29, 42, 38, 0.2)"
          bg="#ffffff"
          color="#1d2a26"
          boxShadow="0 24px 48px rgba(29, 42, 38, 0.15)"
          zIndex={26000}
          _focus={{ outline: "none" }}
        >
          <PopoverBody p="0">
            <Flex direction={{ base: "column", md: "row" }} overflow="hidden" borderRadius="16px">
              <Box p="10px" borderRight="1px solid rgba(29, 42, 38, 0.1)">
                <Calendar
                  onChange={handleDateChange}
                  value={selectedDate || new Date()}
                  minDate={minDate || new Date()}
                  maxDate={maxDate}
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
                <Flex direction="row" bg="#f8fbff" p="14px" gap="14px" minW="180px">
                  {loadingTimeOptions ? (
                    <Flex align="center" justify="center" w="100%" minH="160px">
                      <Text fontSize="13px" color="#5f7398" fontWeight="600">
                        Đang tải khung giờ...
                      </Text>
                    </Flex>
                  ) : hasNoTimeOptions ? (
                    <Flex align="center" justify="center" w="100%" minH="160px">
                      <Text fontSize="13px" color="#5f7398" fontWeight="600" textAlign="center">
                        {noTimeOptionsText}
                      </Text>
                    </Flex>
                  ) : (
                    <>
                      <VStack align="center" spacing="2px">
                        <Text fontSize="xs" fontWeight="700" color="#7fa1c5" mb="5px">
                          GIỜ
                        </Text>
                        <Box h="200px" overflowY="auto" className="hide-scrollbar" w="56px">
                          {hours.map((hour) => (
                            <Button
                              key={hour}
                              size="sm"
                              w="100%"
                              bg={selectedHour === hour ? "#2f6bd8" : "transparent"}
                              color={selectedHour === hour ? "#ffffff" : "#1d2a26"}
                              _hover={{ bg: selectedHour === hour ? "#2f6bd8" : "rgba(47,107,216,0.12)" }}
                              onClick={() => handleHourSelect(hour)}
                              borderRadius="8px"
                              mb="2px"
                            >
                              {hour}
                            </Button>
                          ))}
                        </Box>
                      </VStack>

                      <VStack align="center" spacing="2px">
                        <Text fontSize="xs" fontWeight="700" color="#7fa1c5" mb="5px">
                          PHÚT
                        </Text>
                        <Box h="200px" overflowY="auto" className="hide-scrollbar" w="56px">
                          {minutes.map((minute) => (
                            <Button
                              key={minute}
                              size="sm"
                              w="100%"
                              bg={selectedMinute === minute ? "#2f6bd8" : "transparent"}
                              color={selectedMinute === minute ? "#ffffff" : "#1d2a26"}
                              _hover={{ bg: selectedMinute === minute ? "#2f6bd8" : "rgba(47,107,216,0.12)" }}
                              onClick={() => handleMinuteSelect(minute)}
                              borderRadius="8px"
                              mb="2px"
                            >
                              {minute}
                            </Button>
                          ))}
                        </Box>
                      </VStack>
                    </>
                  )}
                </Flex>
              ) : null}
            </Flex>
          </PopoverBody>
        </PopoverContent>
      </Portal>

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
