import React, { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Box,
  Button,
  Flex,
  FormControl,
  FormLabel,
  Input,
  Select,
  SimpleGrid,
  Spinner,
  Table,
  Tbody,
  Td,
  Text,
  Th,
  Thead,
  Tr,
  useColorModeValue,
  useToast
} from "@chakra-ui/react";
import Card from "components/card/Card";
import MiniStatistics from "components/card/MiniStatistics";
import { useBranch } from "../../../context/BranchContext";
import { useAuth } from "../../../context/AuthContext";
import { getAllStaff } from "../api/staffApi";
import {
  approveInitialSalaryRequest,
  createSalaryProfile,
  deleteSalaryProfile,
  getInitialSalaryRequests,
  getSalaryProfiles,
  rejectInitialSalaryRequest,
  updateSalaryProfile
} from "../api/salaryAdminApi";

function pick(item, ...keys) {
  for (const key of keys) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

function toId(value) {
  const n = Number(value);
  return Number.isFinite(n) && n > 0 ? Math.trunc(n) : null;
}

function toDateInputValue(value) {
  const date = value ? new Date(value) : null;
  if (!date || Number.isNaN(date.getTime())) return "";
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function formatCurrency(value) {
  const amount = Number(value || 0);
  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0
  }).format(Number.isFinite(amount) ? amount : 0);
}

function formatDateTime(value) {
  const date = value ? new Date(value) : null;
  if (!date || Number.isNaN(date.getTime())) return "--";
  return new Intl.DateTimeFormat("vi-VN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  }).format(date);
}

const EMPTY_PROFILE_FORM = {
  staffId: "",
  baseSalary: "",
  effectiveFrom: "",
  effectiveTo: "",
  status: "ACTIVE",
  note: ""
};

export default function AdminSalaryPage() {
  const toast = useToast();
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");
  const { user } = useAuth();
  const { selectedBranchId } = useBranch();
  const branchId = toId(selectedBranchId);

  const isAdminStaff = useMemo(() => {
    const roleCode = String(user?.roleCode || "").trim().toUpperCase();
    const roleName = String(user?.roleName || "").trim().toUpperCase();
    return roleCode === "ADMIN" || roleName.includes("ADMIN");
  }, [user?.roleCode, user?.roleName]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [savingProfile, setSavingProfile] = useState(false);
  const [processingRequestId, setProcessingRequestId] = useState(null);

  const [profiles, setProfiles] = useState([]);
  const [requests, setRequests] = useState([]);
  const [staffs, setStaffs] = useState([]);
  const [requestStatusFilter, setRequestStatusFilter] = useState("PENDING");

  const [editingProfileId, setEditingProfileId] = useState(null);
  const [profileForm, setProfileForm] = useState(EMPTY_PROFILE_FORM);
  const [rejectReasonByRequestId, setRejectReasonByRequestId] = useState({});

  const scopedStaffs = useMemo(() => {
    if (!branchId) return staffs || [];
    return (staffs || []).filter((staff) => {
      const staffBranchId = toId(pick(staff, "branchId", "BranchId"));
      return !staffBranchId || staffBranchId === branchId;
    });
  }, [staffs, branchId]);

  const staffNameById = useMemo(() => {
    const map = new Map();
    scopedStaffs.forEach((staff) => {
      const id = toId(pick(staff, "id", "Id"));
      if (!id) return;
      map.set(id, pick(staff, "fullName", "FullName", "name", "Name") || `Staff #${id}`);
    });
    return map;
  }, [scopedStaffs]);

  const summary = useMemo(() => {
    const pendingRequests = requests.filter((item) => String(pick(item, "status", "Status") || "").toUpperCase() === "PENDING").length;
    const approvedRequests = requests.filter((item) => String(pick(item, "status", "Status") || "").toUpperCase() === "APPROVED").length;
    return {
      profileCount: profiles.length,
      pendingRequests,
      approvedRequests
    };
  }, [profiles, requests]);

  async function loadData() {
    setLoading(true);
    setError("");
    try {
      const [profileData, requestData, staffData] = await Promise.all([
        getSalaryProfiles(branchId),
        getInitialSalaryRequests({
          status: requestStatusFilter,
          branchId
        }),
        getAllStaff()
      ]);

      setProfiles(Array.isArray(profileData) ? profileData : []);
      setRequests(Array.isArray(requestData) ? requestData : []);
      setStaffs(Array.isArray(staffData) ? staffData : []);
    } catch (err) {
      setError(err?.message || "Không tải được dữ liệu lương.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, [branchId, requestStatusFilter]);

  function resetProfileForm() {
    setEditingProfileId(null);
    setProfileForm(EMPTY_PROFILE_FORM);
  }

  function startEditProfile(profile) {
    setEditingProfileId(toId(pick(profile, "id", "Id")));
    setProfileForm({
      staffId: String(toId(pick(profile, "staffId", "StaffId")) || ""),
      baseSalary: String(Number(pick(profile, "baseSalary", "BaseSalary") || 0)),
      effectiveFrom: toDateInputValue(pick(profile, "effectiveFrom", "EffectiveFrom")),
      effectiveTo: toDateInputValue(pick(profile, "effectiveTo", "EffectiveTo")),
      status: String(pick(profile, "status", "Status") || "ACTIVE").toUpperCase(),
      note: String(pick(profile, "note", "Note") || "")
    });
  }

  async function handleSubmitProfile() {
    if (!isAdminStaff) {
      setError("Bạn không có quyền CRUD bảng lương.");
      return;
    }

    const staffId = toId(profileForm.staffId);
    if (!staffId) {
      setError("Vui lòng chọn nhân sự.");
      return;
    }

    const baseSalary = Number(profileForm.baseSalary);
    if (!Number.isFinite(baseSalary) || baseSalary <= 0) {
      setError("Vui lòng nhập lương cơ bản hợp lệ.");
      return;
    }

    if (!profileForm.effectiveFrom) {
      setError("Vui lòng chọn ngày hiệu lực.");
      return;
    }

    setSavingProfile(true);
    setError("");
    try {
      const payload = {
        staffId,
        branchId: branchId || null,
        baseSalary,
        effectiveFrom: profileForm.effectiveFrom,
        effectiveTo: profileForm.effectiveTo || null,
        status: profileForm.status || "ACTIVE",
        note: profileForm.note || null
      };

      if (editingProfileId) {
        await updateSalaryProfile(editingProfileId, payload);
        toast({ status: "success", title: "Đã cập nhật bảng lương nhân sự." });
      } else {
        await createSalaryProfile(payload);
        toast({ status: "success", title: "Đã tạo bảng lương nhân sự." });
      }

      resetProfileForm();
      await loadData();
    } catch (err) {
      setError(err?.message || "Không lưu được bảng lương.");
    } finally {
      setSavingProfile(false);
    }
  }

  async function handleDeleteProfile(profileId) {
    if (!isAdminStaff) {
      setError("Bạn không có quyền xoá bảng lương.");
      return;
    }

    setError("");
    try {
      await deleteSalaryProfile(profileId);
      toast({ status: "success", title: "Đã xoá bảng lương nhân sự." });
      if (editingProfileId === profileId) {
        resetProfileForm();
      }
      await loadData();
    } catch (err) {
      setError(err?.message || "Không xoá được bảng lương.");
    }
  }

  async function handleApproveRequest(requestId) {
    if (!isAdminStaff) {
      setError("Bạn không có quyền duyệt yêu cầu lương.");
      return;
    }

    setProcessingRequestId(requestId);
    setError("");
    try {
      await approveInitialSalaryRequest(requestId, {});
      toast({ status: "success", title: `Đã duyệt yêu cầu #${requestId} và tạo bảng lương.` });
      await loadData();
    } catch (err) {
      setError(err?.message || "Không duyệt được yêu cầu.");
    } finally {
      setProcessingRequestId(null);
    }
  }

  async function handleRejectRequest(requestId) {
    if (!isAdminStaff) {
      setError("Bạn không có quyền từ chối yêu cầu lương.");
      return;
    }

    const rejectReason = (rejectReasonByRequestId[requestId] || "").trim();
    setProcessingRequestId(requestId);
    setError("");
    try {
      await rejectInitialSalaryRequest(requestId, {
        rejectReason: rejectReason || "Không đủ thông tin để duyệt lương."
      });
      toast({ status: "success", title: `Đã từ chối yêu cầu #${requestId}.` });
      setRejectReasonByRequestId((prev) => {
        const next = { ...prev };
        delete next[requestId];
        return next;
      });
      await loadData();
    } catch (err) {
      setError(err?.message || "Không từ chối được yêu cầu.");
    } finally {
      setProcessingRequestId(null);
    }
  }

  if (loading) {
    return (
      <Flex minH='260px' align='center' justify='center' direction='column' gap='10px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Đang tải dữ liệu lương...</Text>
      </Flex>
    );
  }

  return (
    <Box>
      <Box mb='20px'>
        <Text color={textColor} fontSize='2xl' fontWeight='700'>Quản lý lương</Text>
        <Text color={textColorSecondary} fontSize='sm'>
          CRUD bảng lương nhân viên và duyệt yêu cầu tạo lương mới từ kế toán.
        </Text>
      </Box>

      {error ? (
        <Card p='16px' mb='20px'>
          <Text color='red.400' fontWeight='600'>{error}</Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 3 }} gap='20px' mb='20px'>
        <MiniStatistics name='Bảng lương nhân viên' value={String(summary.profileCount)} />
        <MiniStatistics name='Yêu cầu chờ duyệt' value={String(summary.pendingRequests)} />
        <MiniStatistics name='Yêu cầu đã duyệt' value={String(summary.approvedRequests)} />
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 3 }} gap='20px' mb='20px'>
        <Card p='16px'>
          <Text color={textColor} fontWeight='700' mb='10px'>
            {editingProfileId ? "Cập nhật bảng lương" : "Tạo bảng lương nhân viên"}
          </Text>
          {!isAdminStaff ? (
            <Text color='orange.500' fontSize='xs' mb='10px'>
              Tài khoản hiện tại chỉ có quyền xem. Chỉ admin mới được CRUD bảng lương và duyệt yêu cầu.
            </Text>
          ) : null}
          <SimpleGrid columns={1} gap='10px'>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Nhân sự</FormLabel>
              <Select
                value={profileForm.staffId}
                onChange={(e) => setProfileForm((prev) => ({ ...prev, staffId: e.target.value }))}
                isDisabled={!isAdminStaff}>
                <option value=''>Chọn nhân sự</option>
                {scopedStaffs.map((staff) => {
                  const id = toId(pick(staff, "id", "Id"));
                  if (!id) return null;
                  const name = pick(staff, "fullName", "FullName") || `Staff #${id}`;
                  return (
                    <option key={id} value={id}>
                      {`${name} (#${id})`}
                    </option>
                  );
                })}
              </Select>
            </FormControl>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Lương cơ bản</FormLabel>
              <Input
                type='number'
                value={profileForm.baseSalary}
                onChange={(e) => setProfileForm((prev) => ({ ...prev, baseSalary: e.target.value }))}
                placeholder='Ví dụ: 9000000'
                isDisabled={!isAdminStaff}
              />
            </FormControl>
            <SimpleGrid columns={2} gap='10px'>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Hiệu lực từ</FormLabel>
                <Input
                  type='date'
                  value={profileForm.effectiveFrom}
                  onChange={(e) => setProfileForm((prev) => ({ ...prev, effectiveFrom: e.target.value }))}
                  isDisabled={!isAdminStaff}
                />
              </FormControl>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Hiệu lực đến</FormLabel>
                <Input
                  type='date'
                  value={profileForm.effectiveTo}
                  onChange={(e) => setProfileForm((prev) => ({ ...prev, effectiveTo: e.target.value }))}
                  isDisabled={!isAdminStaff}
                />
              </FormControl>
            </SimpleGrid>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Trạng thái</FormLabel>
              <Select
                value={profileForm.status}
                onChange={(e) => setProfileForm((prev) => ({ ...prev, status: e.target.value }))}
                isDisabled={!isAdminStaff}>
                <option value='ACTIVE'>ACTIVE</option>
                <option value='INACTIVE'>INACTIVE</option>
              </Select>
            </FormControl>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Ghi chú</FormLabel>
              <Input
                value={profileForm.note}
                onChange={(e) => setProfileForm((prev) => ({ ...prev, note: e.target.value }))}
                placeholder='Ghi chú bảng lương'
                isDisabled={!isAdminStaff}
              />
            </FormControl>
            <Flex gap='8px'>
              <Button colorScheme='blue' onClick={handleSubmitProfile} isLoading={savingProfile} isDisabled={!isAdminStaff}>
                {editingProfileId ? "Cập nhật" : "Tạo mới"}
              </Button>
              {editingProfileId ? (
                <Button variant='outline' onClick={resetProfileForm}>
                  Huỷ sửa
                </Button>
              ) : null}
            </Flex>
          </SimpleGrid>
        </Card>

        <Card p='16px' gridColumn={{ base: "span 1", xl: "span 2" }}>
          <Text color={textColor} fontWeight='700' mb='10px'>Bảng lương nhân viên</Text>
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>ID</Th>
                <Th>Nhân sự</Th>
                <Th>Lương cơ bản</Th>
                <Th>Hiệu lực</Th>
                <Th>Trạng thái</Th>
                <Th textAlign='right'>Thao tác</Th>
              </Tr>
            </Thead>
            <Tbody>
              {profiles.map((profile, idx) => {
                const id = toId(pick(profile, "id", "Id"));
                const staffId = toId(pick(profile, "staffId", "StaffId"));
                const staffName = pick(profile, "staffName", "StaffName") || staffNameById.get(staffId) || `Staff #${staffId || "--"}`;
                const status = String(pick(profile, "status", "Status") || "").toUpperCase();
                return (
                  <Tr key={id || `p-${idx}`}>
                    <Td>#{id || "--"}</Td>
                    <Td>{staffName}</Td>
                    <Td>{formatCurrency(pick(profile, "baseSalary", "BaseSalary"))}</Td>
                    <Td>
                      <Text fontSize='xs' color={textColorSecondary}>
                        {toDateInputValue(pick(profile, "effectiveFrom", "EffectiveFrom")) || "--"}
                        {" → "}
                        {toDateInputValue(pick(profile, "effectiveTo", "EffectiveTo")) || "Không giới hạn"}
                      </Text>
                    </Td>
                    <Td>
                      <Badge colorScheme={status === "ACTIVE" ? "green" : "gray"}>{status || "--"}</Badge>
                    </Td>
                    <Td textAlign='right'>
                      <Flex justify='flex-end' gap='8px'>
                        <Button size='xs' variant='outline' onClick={() => startEditProfile(profile)} isDisabled={!isAdminStaff}>
                          Sửa
                        </Button>
                        <Button size='xs' colorScheme='red' variant='outline' onClick={() => handleDeleteProfile(id)} isDisabled={!isAdminStaff}>
                          Xoá
                        </Button>
                      </Flex>
                    </Td>
                  </Tr>
                );
              })}
            </Tbody>
          </Table>
        </Card>
      </SimpleGrid>

      <Card p='20px'>
        <Flex justify='space-between' align='center' mb='10px' wrap='wrap' gap='10px'>
          <Text color={textColor} fontWeight='700'>Yêu cầu tạo lương từ kế toán</Text>
          <Select
            value={requestStatusFilter}
            onChange={(e) => setRequestStatusFilter(e.target.value)}
            maxW='200px'>
            <option value='PENDING'>PENDING</option>
            <option value='APPROVED'>APPROVED</option>
            <option value='REJECTED'>REJECTED</option>
            <option value='ALL'>ALL</option>
          </Select>
        </Flex>
        <Table size='sm'>
          <Thead>
            <Tr>
              <Th>Request</Th>
              <Th>Nhân sự</Th>
              <Th>Lương đề xuất</Th>
              <Th>Hiệu lực từ</Th>
              <Th>Người yêu cầu</Th>
              <Th>Trạng thái</Th>
              <Th>Lý do</Th>
              <Th textAlign='right'>Thao tác</Th>
            </Tr>
          </Thead>
          <Tbody>
            {requests.map((item, idx) => {
              const id = toId(pick(item, "id", "Id"));
              const status = String(pick(item, "status", "Status") || "").toUpperCase();
              const isPending = status === "PENDING";
              return (
                <Tr key={id || `r-${idx}`}>
                  <Td>
                    <Text fontWeight='600'>#{id || "--"}</Text>
                    <Text fontSize='xs' color={textColorSecondary}>{formatDateTime(pick(item, "createdAt", "CreatedAt"))}</Text>
                  </Td>
                  <Td>{pick(item, "staffName", "StaffName") || `Staff #${pick(item, "staffId", "StaffId") || "--"}`}</Td>
                  <Td>{formatCurrency(pick(item, "proposedBaseSalary", "ProposedBaseSalary", "amount", "Amount"))}</Td>
                  <Td>{toDateInputValue(pick(item, "effectiveFrom", "EffectiveFrom")) || "--"}</Td>
                  <Td>{pick(item, "requestedByName", "RequestedByName") || `#${pick(item, "requestedBy", "RequestedBy") || "--"}`}</Td>
                  <Td>
                    <Badge colorScheme={status === "APPROVED" ? "green" : status === "REJECTED" ? "red" : "orange"}>
                      {status || "--"}
                    </Badge>
                  </Td>
                  <Td>
                    <Text fontSize='xs' color={textColorSecondary}>
                      {pick(item, "reason", "Reason") || "--"}
                    </Text>
                  </Td>
                  <Td textAlign='right'>
                    <Flex direction='column' gap='6px' align='flex-end'>
                      <Input
                        size='xs'
                        maxW='220px'
                        placeholder='Lý do từ chối (nếu có)'
                        value={rejectReasonByRequestId[id] || ""}
                        onChange={(e) =>
                          setRejectReasonByRequestId((prev) => ({ ...prev, [id]: e.target.value }))
                        }
                        isDisabled={!isPending || !isAdminStaff}
                      />
                      <Flex gap='6px'>
                        <Button
                          size='xs'
                          colorScheme='green'
                          variant='outline'
                          isDisabled={!isPending || !isAdminStaff}
                          isLoading={processingRequestId === id}
                          onClick={() => handleApproveRequest(id)}>
                          Duyệt
                        </Button>
                        <Button
                          size='xs'
                          colorScheme='red'
                          variant='outline'
                          isDisabled={!isPending || !isAdminStaff}
                          isLoading={processingRequestId === id}
                          onClick={() => handleRejectRequest(id)}>
                          Từ chối
                        </Button>
                      </Flex>
                    </Flex>
                  </Td>
                </Tr>
              );
            })}
          </Tbody>
        </Table>
      </Card>
    </Box>
  );
}
