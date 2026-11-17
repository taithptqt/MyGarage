import React from "react";
import { Badge, Box, Button, HStack, SimpleGrid, Text, VStack } from "@chakra-ui/react";
import FilterDropdown from "@shared/components/FilterDropdown";
import {
  formatDateTime,
  getVehicleDisplayParts,
  normalizeSupportRequestMap,
  normalizeSupportingAssignmentMap,
  resolveDiagnosisNumber,
} from "./dispatchOpsHelpers";

export function SupportNotificationCard({ item, onSelect, dispatchTagLabel = "" }) {
  const requestedCount = Math.max(1, Number(item?.requestedTechnicianCount || 1));
  const vehicle = getVehicleDisplayParts(item);

  return (
    <Box
      border="1px solid"
      borderColor="orange.200"
      borderLeftWidth="6px"
      borderLeftColor="orange.400"
      borderRadius="xl"
      bg="white"
      p={4}
      boxShadow="0 8px 24px rgba(245, 158, 11, 0.14)"
      transition="all 0.16s ease"
      _hover={{ transform: "translateY(-2px)", borderColor: "orange.300" }}
    >
      <VStack align="stretch" spacing={3}>
        <HStack justify="space-between" align="start" spacing={2.5} wrap="wrap">
          <HStack spacing={2.5} align="center" flex="1" minW={{ base: "180px", sm: "220px" }}>
            <Box
              display="inline-flex"
              alignItems="center"
              justifyContent="center"
              bg="#ffffff"
              border="2px solid #0f172a"
              borderRadius="md"
              px={2.5}
              py={0.5}
              boxShadow="0 1px 3px rgba(0,0,0,0.12)"
              flexShrink={0}
            >
              <Text
                fontSize={{ base: "md", sm: "lg" }}
                fontWeight="900"
                letterSpacing="0.05em"
                color="#0f172a"
                lineHeight="1.1"
              >
                {vehicle.plate || "CHƯA BIỂN"}
              </Text>
            </Box>

            <Box minW={0}>
              <Text
                fontSize={{ base: "sm", sm: "md" }}
                fontWeight="800"
                color="#1e293b"
                lineHeight="1.2"
                noOfLines={1}
              >
                {vehicle.descriptor || "Xe dịch vụ"}
              </Text>
              <Text fontSize="xs" color="gray.500" noOfLines={1}>
                {item.serviceOrderCode ? `#${item.serviceOrderCode.slice(-4)}` : ""} {item.customerName ? `· ${item.customerName}` : ""}
              </Text>
            </Box>
          </HStack>

          <Badge colorScheme="orange" variant="solid" borderRadius="full" px={2.5} py={0.8} fontSize="xs" fontWeight="800">
            Cần {requestedCount} thợ hỗ trợ
          </Badge>
        </HStack>

        <SimpleGrid columns={{ base: 1, md: 2 }} spacing={2}>
          <Box p={2.5} border="1px solid" borderColor="orange.100" borderRadius="lg" bg="orange.50">
            <Text fontSize="xs" color="orange.700" fontWeight="700" textTransform="uppercase">
              Tổ yêu cầu hỗ trợ
            </Text>
            <Text mt={1} fontSize="sm" color="gray.700" fontWeight="700">
              {item.primaryTeamName || item.teamName || "Chưa xác định"}
            </Text>
            <Text mt={1} fontSize="sm" color="gray.600">
              Cần {requestedCount} thợ từ tổ của bạn
            </Text>
          </Box>
          <Box p={2.5} border="1px solid" borderColor="gray.100" borderRadius="lg" bg="gray.50">
            <Text fontSize="xs" color="gray.600" fontWeight="700" textTransform="uppercase">
              Thời điểm yêu cầu
            </Text>
            <Text mt={1} fontSize="sm" color="gray.700" fontWeight="700">
              {formatDateTime(item.supportRequestedAt || item.requestedAt)}
            </Text>
            <Text mt={1} fontSize="xs" color="gray.500">
              Chuyên môn: {dispatchTagLabel || getDispatchTagLabel(item.dispatchTag)}
            </Text>
          </Box>
        </SimpleGrid>

        <Box p={2.5} border="1px dashed" borderColor="orange.200" borderRadius="lg" bg="#fffbeb">
          <Text fontSize="xs" color="orange.700" fontWeight="700" textTransform="uppercase" mb={1}>
            Ghi chú từ tổ chính
          </Text>
          <Text fontSize="sm" color="gray.700">
            {toText(item.supportRequestNote) || "Không có ghi chú bổ sung."}
          </Text>
        </Box>

        <HStack justify="flex-end">
          <Button
            size="sm"
            colorScheme="orange"
            borderRadius="full"
            onClick={() => onSelect(item.serviceOrderId)}
          >
            Vào chọn thợ hỗ trợ
          </Button>
        </HStack>
      </VStack>
    </Box>
  );
}

export function SupportingTeamSelector({
  mode = "assignment",
  value,
  onChange,
  options,
  assignmentMap,
  onAssignmentChange,
  technicianOptionsByTeam,
  requestMap,
  onRequestedCountChange,
  emptyText,
  helperText,
}) {
  const isRequestMode = mode === "request";
  const selectedValues = Array.isArray(value) ? value.map((item) => String(item)) : [];
  const selectedSet = new Set(selectedValues);
  const safeOptions = Array.isArray(options) ? options : [];
  const safeAssignmentMap = normalizeSupportingAssignmentMap(assignmentMap);
  const safeRequestMap = normalizeSupportRequestMap(requestMap);
  const safeTechnicianOptionsByTeam = technicianOptionsByTeam && typeof technicianOptionsByTeam === "object"
    ? technicianOptionsByTeam
    : {};
  const selectedTeams = safeOptions.filter((team) => selectedSet.has(String(team.id)));
  const requestedCountOptions = Array.from({ length: 8 }, (_, index) => ({
    value: String(index + 1),
    label: `${index + 1} thợ`,
  }));

  const handleToggle = (teamId) => {
    const normalizedTeamId = String(teamId);
    const next = selectedSet.has(normalizedTeamId)
      ? selectedValues.filter((item) => item !== normalizedTeamId)
      : [...selectedValues, normalizedTeamId];
    if (typeof onChange === "function") {
      onChange(next);
    }
  };

  return (
    <Box border="1px solid" borderColor="gray.200" borderRadius="xl" p={3} bg="white">
      <HStack justify="space-between" mb={2}>
        <Text fontSize="sm" color="gray.700" fontWeight="700">
          {isRequestMode ? "Chọn tổ phụ cần hỗ trợ" : "Chọn tổ phối hợp"}
        </Text>
        <Badge colorScheme={selectedValues.length > 0 ? (isRequestMode ? "orange" : "blue") : "gray"} borderRadius="full">
          {selectedValues.length} tổ
        </Badge>
      </HStack>

      {safeOptions.length === 0 ? (
        <Text fontSize="sm" color="gray.500">{emptyText}</Text>
      ) : (
        <VStack align="stretch" spacing={2}>
          {safeOptions.map((team) => {
            const optionValue = String(team.id);
            const isSelected = selectedSet.has(optionValue);
            return (
              <Box
                key={`support-team-option-${team.id}`}
                as="button"
                type="button"
                w="100%"
                px={3}
                py={2.5}
                borderRadius="lg"
                border="1px solid"
                borderColor={isSelected ? "blue.300" : "gray.200"}
                bg={isSelected ? "blue.50" : "gray.50"}
                _hover={{ borderColor: isSelected ? "blue.400" : "gray.300", bg: isSelected ? "blue.50" : "gray.100" }}
                transition="all 0.15s ease"
                onClick={() => handleToggle(optionValue)}
              >
                <HStack justify="space-between" spacing={3}>
                  <HStack spacing={3} minW={0}>
                    <Box
                      w="18px"
                      h="18px"
                      borderRadius="md"
                      border="1.5px solid"
                      borderColor={isSelected ? "blue.500" : "gray.400"}
                      bg={isSelected ? "blue.500" : "white"}
                      display="inline-flex"
                      alignItems="center"
                      justifyContent="center"
                      color="white"
                      fontSize="11px"
                      fontWeight="700"
                      flexShrink={0}
                    >
                      {isSelected ? "✓" : ""}
                    </Box>
                    <Text
                      fontSize="sm"
                      color="gray.800"
                      fontWeight={isSelected ? "700" : "600"}
                      textAlign="left"
                      whiteSpace="normal"
                    >
                      {team.name}
                    </Text>
                  </HStack>
                  {isSelected && (
                    <Badge colorScheme="blue" borderRadius="full" px={2}>
                      Đã chọn
                    </Badge>
                  )}
                </HStack>
              </Box>
            );
          })}
        </VStack>
      )}

      {selectedTeams.length > 0 && (
        <Box mt={3}>
          <Divider mb={3} />
          <VStack align="stretch" spacing={3}>
            {selectedTeams.map((team) => {
              const teamId = String(team.id);
              if (isRequestMode) {
                const request = safeRequestMap[teamId] || { requestedTechnicianCount: "1" };
                const requestedCount = String(
                  Math.max(
                    1,
                    Math.min(8, Number(readValue(request, "requestedTechnicianCount", "RequestedTechnicianCount") || 1))
                  )
                );

                return (
                  <Box
                    key={`support-team-request-${team.id}`}
                    border="1px solid"
                    borderColor="orange.200"
                    borderRadius="lg"
                    p={3}
                    bg="#fffaf0"
                  >
                    <SimpleGrid columns={{ base: 1, md: 2 }} spacing={2.5} alignItems="end">
                      <Box>
                        <Text fontSize="sm" fontWeight="700" color="gray.700">
                          {team.name}
                        </Text>
                        <Text mt={1} fontSize="xs" color="gray.500">
                          Tổ trưởng tổ này sẽ tự chọn thợ sau khi nhận yêu cầu.
                        </Text>
                      </Box>
                      <FormControl maxW="160px" justifySelf={{ base: "stretch", md: "end" }}>
                        <FormLabel fontSize="xs" mb={1} color="gray.600">
                          Số lượng thợ cần hỗ trợ
                        </FormLabel>
                        <FilterDropdown
                          id={`support-team-request-count-${team.id}`}
                          value={requestedCount}
                          options={requestedCountOptions}
                          onChange={(nextValue) => onRequestedCountChange?.(team.id, nextValue)}
                          minWidth={120}
                          menuMinWidth={120}
                        />
                      </FormControl>
                    </SimpleGrid>
                  </Box>
                );
              }

              const assignment = safeAssignmentMap[teamId] || { technicianId: "", assistantTechnicianId: "" };
              const teamOptions = safeTechnicianOptionsByTeam[teamId] || {};
              const leadOptions = [
                { value: "", label: "-- Chọn thợ chính --" },
                ...(Array.isArray(teamOptions.lead) ? teamOptions.lead : []),
              ];
              const selectedLeadId = toPositiveNumber(assignment.technicianId);
              const selectedAssistantId = toPositiveNumber(assignment.assistantTechnicianId);
              const assistantOptions = Array.isArray(teamOptions.assistant) && teamOptions.assistant.length > 0
                ? teamOptions.assistant.filter((item) => {
                  const optionId = toPositiveNumber(item?.value);
                  if (!optionId) return true;
                  if (!selectedLeadId) return true;
                  return optionId !== selectedLeadId;
                })
                : [{ value: "", label: "-- Không chọn --" }];
              const safeAssistantValue =
                selectedLeadId && selectedAssistantId && selectedLeadId === selectedAssistantId
                  ? ""
                  : (assignment.assistantTechnicianId || "");
              const hasLeadCandidate = leadOptions.some((item) => toPositiveNumber(item.value));

              return (
                <Box
                  key={`support-team-assignment-${team.id}`}
                  border="1px solid"
                  borderColor="gray.200"
                  borderRadius="lg"
                  p={3}
                  bg="gray.50"
                >
                  <Text fontSize="sm" fontWeight="700" color="gray.700" mb={2}>
                    {team.name}
                  </Text>
                  <SimpleGrid columns={{ base: 1, md: 2 }} spacing={2.5}>
                    <FormControl>
                      <FormLabel fontSize="xs" mb={1} color="gray.600">
                        Thợ chính
                      </FormLabel>
                      <FilterDropdown
                        id={`support-team-lead-${team.id}`}
                        value={assignment.technicianId || ""}
                        options={leadOptions}
                        onChange={(nextValue) => {
                          onAssignmentChange?.(team.id, "technicianId", nextValue);
                          const nextLeadId = toPositiveNumber(nextValue);
                          const currentAssistantId = toPositiveNumber(assignment.assistantTechnicianId);
                          if (nextLeadId && currentAssistantId && nextLeadId === currentAssistantId) {
                            onAssignmentChange?.(team.id, "assistantTechnicianId", "");
                          }
                        }}
                        minWidth="100%"
                        disabled={!hasLeadCandidate}
                      />
                    </FormControl>
                    <FormControl>
                      <FormLabel fontSize="xs" mb={1} color="gray.600">
                        Thợ phụ
                      </FormLabel>
                      <FilterDropdown
                        id={`support-team-assistant-${team.id}`}
                        value={safeAssistantValue}
                        options={assistantOptions}
                        onChange={(nextValue) => {
                          const nextAssistantId = toPositiveNumber(nextValue);
                          if (selectedLeadId && nextAssistantId && nextAssistantId === selectedLeadId) {
                            onAssignmentChange?.(team.id, "assistantTechnicianId", "");
                            return;
                          }
                          onAssignmentChange?.(team.id, "assistantTechnicianId", nextValue);
                        }}
                        minWidth="100%"
                        disabled={!hasLeadCandidate}
                      />
                    </FormControl>
                  </SimpleGrid>
                </Box>
              );
            })}
          </VStack>
        </Box>
      )}

      <Text fontSize="xs" color="gray.500" mt={2}>
        {helperText}
      </Text>
    </Box>
  );
}

