/*
 * usbhid2can.c
 *
 *  Created on: Aug 22, 2025
 *      Author: Hoang
 */

#include "usbhid2can.h"

CAN_TxHeaderTypeDef g_CanTxHeader;
CAN_FilterTypeDef  	g_CanFilter;
uint32_t g_u32TxMailbox;
HID_FrameFIFO_t g_HIDFrameFIFO_Receive;
volatile uint32_t g_u32_GetErrCan = 0  ;
extern HID_FrameFIFO_t g_HIDFrameFIFO_Tranfer;
extern TIM_HandleTypeDef htim4;
extern TIM_HandleTypeDef htim5;
#define SAMPLE_POINT_SCALE 1000U
#define HEADER
typedef struct {
    int tq_total;
    int prescaler;
    int tseg1;
    int tseg2;
    float actual_sample_point;
    float sample_point_error;
} CAN_TimingConfig;

//#define HAL_CAN_ERROR_NONE            (0x00000000U)  /*!< No error                                             */
//#define HAL_CAN_ERROR_EWG             (0x00000001U)  /*!< Protocol Error Warning                               */
//#define HAL_CAN_ERROR_EPV             (0x00000002U)  /*!< Error Passive                                        */
//#define HAL_CAN_ERROR_BOF             (0x00000004U)  /*!< Bus-off error                                        */
//#define HAL_CAN_ERROR_STF             (0x00000008U)  /*!< Stuff error                                          */
//#define HAL_CAN_ERROR_FOR             (0x00000010U)  /*!< Form error                                           */
//#define HAL_CAN_ERROR_ACK             (0x00000020U)  /*!< Acknowledgment error                                 */
//#define HAL_CAN_ERROR_BR              (0x00000040U)  /*!< Bit recessive error                                  */
//#define HAL_CAN_ERROR_BD              (0x00000080U)  /*!< Bit dominant error                                   */
//#define HAL_CAN_ERROR_CRC             (0x00000100U)  /*!< CRC error                                            */
//#define HAL_CAN_ERROR_RX_FOV0         (0x00000200U)  /*!< Rx FIFO0 overrun error                               */
//#define HAL_CAN_ERROR_RX_FOV1         (0x00000400U)  /*!< Rx FIFO1 overrun error                               */
//#define HAL_CAN_ERROR_TX_ALST0        (0x00000800U)  /*!< TxMailbox 0 transmit failure due to arbitration lost */
//#define HAL_CAN_ERROR_TX_TERR0        (0x00001000U)  /*!< TxMailbox 0 transmit failure due to transmit error   */
//#define HAL_CAN_ERROR_TX_ALST1        (0x00002000U)  /*!< TxMailbox 1 transmit failure due to arbitration lost */
//#define HAL_CAN_ERROR_TX_TERR1        (0x00004000U)  /*!< TxMailbox 1 transmit failure due to transmit error   */
//#define HAL_CAN_ERROR_TX_ALST2        (0x00008000U)  /*!< TxMailbox 2 transmit failure due to arbitration lost */
//#define HAL_CAN_ERROR_TX_TERR2        (0x00010000U)  /*!< TxMailbox 2 transmit failure due to transmit error   */
//#define HAL_CAN_ERROR_TIMEOUT         (0x00020000U)  /*!< Timeout error                                        */
//#define HAL_CAN_ERROR_NOT_INITIALIZED (0x00040000U)  /*!< Peripheral not initialized                           */
//#define HAL_CAN_ERROR_NOT_READY       (0x00080000U)  /*!< Peripheral not ready                                 */
//#define HAL_CAN_ERROR_NOT_STARTED     (0x00100000U)  /*!< Peripheral not started                               */
//#define HAL_CAN_ERROR_PARAM           (0x00200000U)  /*!< Parameter error                                      */

typedef struct{
	uint8_t TimerRxCanStart:1;
	uint8_t TimestempStart:1;
	uint8_t ConfigBaudrate:1;
	uint8_t ConfigFilter:1;
	uint8_t CanStart:1;
	uint8_t TimerRxCanStop:1;
	uint8_t TimestempStop:1;
	uint8_t CanStop:1;
}CAN_ConfigStatus_t;

typedef union{
	CAN_ConfigStatus_t CAN_ConfigStatus;
	uint8_t CheckCanConfigStatus;
}CAN_ConfigStatus_u;

typedef struct{
	uint16_t CanErrEWG:1; /*!< Protocol Error Warning                               */
	uint16_t CanErrEPV:1; /*!< Error Passive                                        */
	uint16_t CanErrBOF:1; /*!< Bus-off error                                        */
	uint16_t CanErrSTF:1; /*!< Stuff error                                          */
	uint16_t CanErrFOR:1; /*!< Form error                                           */
}CAN_MessageStatus_t;

typedef union{
	CAN_MessageStatus_t CAN_MessageStatus;
	uint8_t CheckCanMessageStatus;
}CAN_MessageStatus_u;

CAN_ConfigStatus_u CAN_ConfigStatus = {HAL_OK};
CAN_MessageStatus_t CAN_MessageStatus = {HAL_OK};


HAL_StatusTypeDef CanTx_init(uint32_t id, uint8_t dlc, uint8_t *data);
HAL_StatusTypeDef CanRx_FilterRange(uint32_t start_id, uint32_t end_id, uint8_t is_extended);

HAL_StatusTypeDef SendCanConfig(uint8_t *data);
HAL_StatusTypeDef SendCanConfigConnect(uint8_t *data);
HAL_StatusTypeDef SendCanConfigDisconnect(uint8_t *data);
HAL_StatusTypeDef SendCanConfigBaud(uint8_t *data);
HAL_StatusTypeDef SendCanConfigFilter(uint8_t *data);
CAN_TimingConfig find_best_timing(uint32_t baudrate, uint16_t desired_sample_point);

HAL_StatusTypeDef SendCanMessage(uint8_t *data);
HAL_StatusTypeDef (*g_PtrFunc_SendCan[3])(uint8_t *data) = {0,SendCanConfig,SendCanMessage};
//HAL_StatusTypeDef



/**
 * Usb2Can_Tranfer
 *  - Đọc frame từ FIFO
 *  - Gọi hàm gửi CAN tương ứng
 *  - Nếu gửi thành công → đánh dấu frame đã đọc (tail tiến)
 *  - Nếu gửi không thành công → tail giữ nguyên, sẽ gửi lại lần sau
 *
 * Trả về:
 *  0: FIFO rỗng
 *  1: frame đã gửi thành công
 *  2: frame chưa gửi (CAN bận hoặc lỗi)
 */

HAL_StatusTypeDef Usb2Can_Tranfer(HID_FrameFIFO_t *fifo)
{
    uint8_t l_au8DataUsb[HID_FRAME_SIZE] = {0} ;

    // Kiểm tra FIFO rỗng
    if(fifo->head == fifo->tail){
        return HAL_ERROR;
    }

    // Copy frame ra buffer tạm
    memcpy(l_au8DataUsb, fifo->frame[fifo->tail], HID_FRAME_SIZE);

    // Gọi hàm gửi CAN tương ứng
 //   uint8_t sendResult = g_PtrFunc_SendCan[l_au8DataUsb[0]](l_au8DataUsb);
 //   if(sendResult ==  HAL_OK) {
    if(g_PtrFunc_SendCan[l_au8DataUsb[0]](l_au8DataUsb) != HAL_OK){
    // Gửi thành công → đánh dấu đã đọc
        return HAL_BUSY;
    }
    fifo->tail = (fifo->tail + 1) % HID_FRAME_BUFFER_SIZE;
    return HAL_OK;

}




void delay_0_1ms_tim5(void)
{
    uint32_t start = TIM5->CNT;//__HAL_TIM_GET_COUNTER(&htim5);
    uint32_t ticks = 50; // 0.1ms / 0.1us = 1000 ticks

 //   while((__HAL_TIM_GET_COUNTER(&htim5) - start) < ticks)
    while(((TIM5->CNT) - start) < ticks)
    {
        // chờ đủ số tick
    }
}
HAL_StatusTypeDef SendCanConfig(uint8_t *data){
	switch(data[1]){
	case 0 :
		//SendCanConfigDisconnect(data) ;
		if(SendCanConfigDisconnect(data) != HAL_OK) {
			data[12] = CAN_ConfigStatus.CheckCanConfigStatus;
		//	printf("Loi disconect!!, gia tri loi : %d\r\n", CAN_ConfigStatus.CheckCanConfigStatus);
			HID_Frame_Write1(&g_HIDFrameFIFO_Tranfer,data);
			return HAL_ERROR;
		}
		break;
	default:
		//SendCanConfigConnect(data);
		if(SendCanConfigConnect(data) != HAL_OK ){
			data[12] =  CAN_ConfigStatus.CheckCanConfigStatus;
		//	printf("Loi conect!! , gia tri loi : %d\r\n", CAN_ConfigStatus.CheckCanConfigStatus);
			HID_Frame_Write1(&g_HIDFrameFIFO_Tranfer,data);
			return HAL_ERROR;
		}
		break;
	}
	return HAL_OK;
}
HAL_StatusTypeDef SendCanConfigDisconnect(uint8_t *data){
	  // 1. Vào Init mode
//	  SET_BIT(hcan1.Instance->MCR, CAN_MCR_INRQ);
//	  while ((hcan1.Instance->MSR & CAN_MSR_INAK) == 0);
//
//	  // 2. Reset error counters
//	  hcan1.Instance->ESR = 0;
//
//	  // 3. Thoát Init mode → về Normal mode
//	  CLEAR_BIT(hcan1.Instance->MCR, CAN_MCR_INRQ);
//	  while ((hcan1.Instance->MSR & CAN_MSR_INAK) != 0);
//
//	  // 4. Dọn mailbox
//	  HAL_CAN_AbortTxRequest(&hcan1,
//	      CAN_TX_MAILBOX0 | CAN_TX_MAILBOX1 | CAN_TX_MAILBOX2);
//
//	  // 5. Reset ErrorCode trong HAL handle
//	  hcan1.ErrorCode = HAL_CAN_ERROR_NONE;

//	 	 	 	      	SET_BIT(hcan1.Instance->MCR, CAN_MCR_INRQ);
//	 	 	 	      	while (!(hcan1.Instance->MSR & CAN_MSR_INAK)); // đợi Init mode
//	 	 	 	      //	hcan1.ErrorCode = 0;
//	 	 	 	      	CLEAR_BIT(hcan1.Instance->MCR, CAN_MCR_INRQ);
//	 	 	 	      	while (hcan1.Instance->MSR & CAN_MSR_INAK); // đợi ready

	  if ( HAL_CAN_DeInit(&hcan1) != HAL_OK)  //HAL_CAN_Stop(&hcan1) != HAL_OK  ||
	  {
		 CAN_ConfigStatus.CAN_ConfigStatus.CanStop = HAL_ERROR ;
	     return HAL_ERROR;
	  }

	  if(HAL_TIM_Base_Stop_IT(&htim4) != HAL_OK )
	  {
		  CAN_ConfigStatus.CAN_ConfigStatus.TimerRxCanStop = HAL_ERROR ;
		  return HAL_ERROR;
	  }
	  if( HAL_TIM_Base_Stop(&htim5) != HAL_OK){
		  CAN_ConfigStatus.CAN_ConfigStatus.TimestempStop = HAL_ERROR ;
		  return HAL_ERROR;
	  }
//	  HAL_CAN_StateTypeDef st = HAL_CAN_GetState(&hcan1);
//	  	  uint32_t err = hcan1.ErrorCode;
//	  	  uint32_t esr = hcan1.Instance->ESR;   // Error Status Register
//	  	  uint8_t TEC = esr & 0xFF;
//	  	  uint8_t REC = (esr >> 8) & 0xFF;
//
//	  	  printf("sau khi disconnect : HAL state=%u, ErrorCode=0x%08lX, ESR=0x%08lX, TEC=%u, REC=%u\r\n",
//	  	         st, err, esr, TEC, REC);



//		HAL_CAN_ResetError(&hcan1);
//		HAL_CAN_AbortTxRequest(&hcan1, CAN_TX_MAILBOX0 | CAN_TX_MAILBOX1 | CAN_TX_MAILBOX2);


	  return HAL_OK;
}

HAL_StatusTypeDef SendCanConfigConnect(uint8_t *data){


	  if(SendCanConfigBaud(data) != HAL_OK){
		  CAN_ConfigStatus.CAN_ConfigStatus.ConfigBaudrate =  HAL_ERROR;
		  return HAL_ERROR;
	  }
	  if(SendCanConfigFilter(data) != HAL_OK ){
		  CAN_ConfigStatus.CAN_ConfigStatus.ConfigFilter =  HAL_ERROR;
		  return HAL_ERROR;
	  }
	  if(HAL_CAN_Start(&hcan1) != HAL_OK ){
		  CAN_ConfigStatus.CAN_ConfigStatus.CanStart =  HAL_ERROR;
		  return HAL_ERROR;
	  }
	if(HAL_TIM_Base_Start_IT(&htim4) !=  HAL_OK){
		CAN_ConfigStatus.CAN_ConfigStatus.TimerRxCanStart =  HAL_ERROR;
		return HAL_ERROR;
	}
	  if(HAL_TIM_Base_Start(&htim5) != HAL_OK){
		  CAN_ConfigStatus.CAN_ConfigStatus.TimestempStart =  HAL_ERROR;
		  return HAL_ERROR;
	  }
//	  HAL_CAN_StateTypeDef st = HAL_CAN_GetState(&hcan1);
//	  uint32_t err = hcan1.ErrorCode;
//	  uint32_t esr = hcan1.Instance->ESR;   // Error Status Register
//	  uint8_t TEC = esr & 0xFF;
//	  uint8_t REC = (esr >> 8) & 0xFF;
//
//	  printf("sau khi config : HAL state=%u, ErrorCode=0x%08lX, ESR=0x%08lX, TEC=%u, REC=%u\r\n",
//	         st, err, esr, TEC, REC);
	  return HAL_OK;
}


HAL_StatusTypeDef SendCanConfigBaud(uint8_t *data){
	uint32_t baudrate = ((data[2] << 8) | data[1]) * 1000;
	uint16_t desired_sample_point = (data[4] << 8) | data[3];
    CAN_TimingConfig config = find_best_timing(baudrate, desired_sample_point);
	hcan1.Instance = CAN1;
	hcan1.Init.Prescaler = config.prescaler;
	hcan1.Init.Mode = CAN_MODE_NORMAL;
	hcan1.Init.SyncJumpWidth = CAN_SJW_1TQ;
	hcan1.Init.TimeSeg1 = (config.tseg1 - 1 ) << 16;
	hcan1.Init.TimeSeg2 = (config.tseg2 - 1 ) << 20;
	hcan1.Init.TimeTriggeredMode = DISABLE;
	hcan1.Init.AutoBusOff = ENABLE;
	hcan1.Init.AutoWakeUp = DISABLE;
	hcan1.Init.AutoRetransmission = DISABLE;
	hcan1.Init.ReceiveFifoLocked = DISABLE;
	hcan1.Init.TransmitFifoPriority = DISABLE;
	if (HAL_CAN_Init(&hcan1) != HAL_OK)
	{
		return HAL_ERROR;
	}
	return HAL_OK;
}

/*
 * Function: find_best_timing
 * ---------------------------
 * Tìm cấu hình CAN tốt nhất (prescaler, tseg1, tseg2) để đạt được baudrate mong muốn
 * và sample point gần với giá trị yêu cầu nhất (dưới dạng fixed-point).
 *
 * Input:
 *   - baudrate: Tốc độ truyền CAN mong muốn (bit/s)
 *   - desired_sample_point_scaled: Sample point mong muốn đã nhân với 1000
 *       (ví dụ: 0.875 → truyền vào 875)
 *
 * Output:
 *   - Trả về cấu trúc CAN_TimingConfig với cấu hình tốt nhất đã tìm được
 *
 * Sequence:
 *   [1] Duyệt tổng số Time Quanta (TQ) từ 8 đến 25 (theo chuẩn CAN)
 *   [2] Với mỗi giá trị tq_total:
 *       [2.1] Kiểm tra xem tq_total có chia hết (PCLK1 / baudrate) không
 *             → Nếu không chia hết thì bỏ qua
 *       [2.2] Tính prescaler: prescaler = (PCLK1 / baudrate) / tq_total
 *       [2.3] Duyệt các cặp giá trị tseg1 (1..16) và tseg2 (1..8):
 *             [2.3.1] Nếu (1 + tseg1 + tseg2) == tq_total:
 *                 - Tính sample point thực tế: actual_sp = (1 + tseg1) / tq_total
 *                 - Chuyển actual_sp thành fixed-point: actual_sp_scaled = actual_sp * 1000
 *                 - So sánh sai số với desired_sample_point_scaled
 *                 - Nếu sai số nhỏ hơn trước đó → lưu lại cấu hình tốt nhất
 *   [3] Trả về cấu hình có sai số nhỏ nhất với sample point mong muốn
 */
CAN_TimingConfig find_best_timing(uint32_t baudrate, uint16_t desired_sample_point_scaled)
{
    CAN_TimingConfig best_config = {0};
    best_config.sample_point_error = FLT_MAX;

    for (int tq_total = 8; tq_total <= 25; tq_total++) {
        if ((HAL_RCC_GetPCLK1Freq() / baudrate) % tq_total != 0)
            continue;

        int prescaler = (HAL_RCC_GetPCLK1Freq() / baudrate) / tq_total;

        for (int tseg1 = 1; tseg1 <= 16; tseg1++) {
            for (int tseg2 = 1; tseg2 <= 8; tseg2++) {
                if (1 + tseg1 + tseg2 != tq_total)
                    continue;

                float actual_sp = (1.0f + tseg1) / tq_total;
                uint16_t actual_sp_scaled = (uint16_t)(actual_sp * SAMPLE_POINT_SCALE);

                float error = fabsf((float)(actual_sp_scaled - desired_sample_point_scaled) / SAMPLE_POINT_SCALE);

                if (error < best_config.sample_point_error) {
                    best_config.tq_total = tq_total;
                    best_config.prescaler = prescaler;
                    best_config.tseg1 = tseg1;
                    best_config.tseg2 = tseg2;
                    best_config.actual_sample_point = actual_sp;
                    best_config.sample_point_error = error;
                }
            }
        }
    }

    return best_config;
}

HAL_StatusTypeDef SendCanConfigFilter(uint8_t *data){
	uint32_t start_id =  (data[9] << 24 ) | (data[8] << 16 ) | (data[7] << 8 ) | data[6];
	uint32_t end_id = (data[13] << 24 ) | (data[12] << 16 ) | (data[11] << 8 ) | data[10];
	if(CanRx_FilterRange(start_id, end_id, data[5]) != HAL_OK ){
		return HAL_ERROR;
	}
	return HAL_OK;
}
HAL_StatusTypeDef CanRx_FilterRange(uint32_t start_id, uint32_t end_id, uint8_t is_extended)
{
    uint32_t range = end_id - start_id + 1;

    // Kiểm tra range có phải là lũy thừa của 2
    if ((range & (range - 1)) != 0) {
        return HAL_ERROR; // Không phải lũy thừa của 2
    }

    uint32_t mask, id_filter;

    if (is_extended == 0) {
        // Standard ID (11-bit)
        if (start_id > 0x7FF || end_id > 0x7FF) return HAL_ERROR;

        mask = 0x7FF & ~(range - 1);

        if ((start_id & ~mask) != 0) return HAL_ERROR;

        id_filter = start_id << 5;
        mask <<= 5;

        g_CanFilter.FilterIdHigh = (uint16_t)(id_filter);
        g_CanFilter.FilterIdLow  = 0x0000;
        g_CanFilter.FilterMaskIdHigh = (uint16_t)(mask);
        g_CanFilter.FilterMaskIdLow  = 0x0000;
    } else {
        // Extended ID (29-bit)
        if (start_id > 0x1FFFFFFF || end_id > 0x1FFFFFFF) return HAL_ERROR;

        mask = 0x1FFFFFFF & ~(range - 1);

        if ((start_id & ~mask) != 0) return HAL_ERROR;

        id_filter = (start_id << 3) | (1 << 2);  // IDE bit = 1 in ID field
        mask = (mask << 3) | (1 << 2);           // Mask includes IDE match

        g_CanFilter.FilterIdHigh = (uint16_t)(id_filter >> 16);
        g_CanFilter.FilterIdLow  = (uint16_t)(id_filter & 0xFFFF);
        g_CanFilter.FilterMaskIdHigh = (uint16_t)(mask >> 16);
        g_CanFilter.FilterMaskIdLow  = (uint16_t)(mask & 0xFFFF);
    }
    g_CanFilter.FilterBank = 0;
    g_CanFilter.FilterMode = CAN_FILTERMODE_IDMASK;
    g_CanFilter.FilterScale = CAN_FILTERSCALE_32BIT;
    g_CanFilter.FilterFIFOAssignment = CAN_RX_FIFO0;
    g_CanFilter.FilterActivation = ENABLE;

    if (HAL_CAN_ConfigFilter(&hcan1, &g_CanFilter) != HAL_OK ||
    HAL_CAN_ActivateNotification(&hcan1, INTERNAL_CAN_IT_FLAGS) != HAL_OK){
    	return HAL_ERROR;
    }
    return HAL_OK;
}



HAL_StatusTypeDef SendCanMessage(uint8_t *data){
	uint32_t id = (data[1]<< 24) |(data[2]<< 16) |(data[3]<< 8) | data[4];
	if(CanTx_init(id, data[5], &data[6]) != HAL_OK ){
		return HAL_ERROR;
	}
	 // printf("Ra khoi ham truyen: ErrorCode=0x%08lX\r\n", g_u32_GetErrCan);
	if( g_u32_GetErrCan != HAL_OK){
			data[14] = (g_u32_GetErrCan >> 24) & 0xFF;
			data[15] = (g_u32_GetErrCan >> 16) & 0xFF;
			data[16]= (g_u32_GetErrCan >> 8) & 0xFF;
			data[17] = (g_u32_GetErrCan ) & 0xFF;
			HID_Frame_Write1(&g_HIDFrameFIFO_Tranfer,data);
			if (g_u32_GetErrCan & HAL_CAN_ERROR_BOF){
				return HAL_OK;
			 	    }
			GPIOA->ODR ^= 1<<7 ;
			g_u32_GetErrCan = 0;
			return HAL_ERROR;
		}
	g_u32_GetErrCan = 0;
	GPIOA->ODR ^= 1<<6 ;
	return HAL_OK;
}
HAL_StatusTypeDef CanTx_init(uint32_t id, uint8_t DlcAndType, uint8_t *data){
	//uint32_t txMailbox;
    switch(DlcAndType & 0x0F){
    case CAN_ID_EXT:
    	g_CanTxHeader.IDE = CAN_ID_EXT;
    	g_CanTxHeader.ExtId = id;
    	break;
    case CAN_ID_STD:
    	g_CanTxHeader.IDE = CAN_ID_STD;
    	g_CanTxHeader.StdId = id;
    	break;
    }
	g_CanTxHeader.RTR = CAN_RTR_DATA;
	g_CanTxHeader.DLC = (DlcAndType >> 4);
	g_CanTxHeader.TransmitGlobalTime = DISABLE;
	if( g_u32_GetErrCan != HAL_OK){
		//printf("loi trong ham truyen: ErrorCode=0x%08lX\r\n", g_u32_GetErrCan);
		if (g_u32_GetErrCan & HAL_CAN_ERROR_BOF){
						return HAL_OK;
					 	    }

		return HAL_ERROR;
	}
 	 // printf("Da vao ham truyen: ErrorCode=0x%08lX\r\n",
 	 //        g_u32_GetErrCan);
	HAL_CAN_AddTxMessage(&hcan1, &g_CanTxHeader, data, &g_u32TxMailbox);
	delay_0_1ms_tim5();
	return HAL_OK;
}







uint8_t HID_Frame_Write(HID_FrameFIFO_t *fifo, uint8_t *data)
{
    uint8_t nextHead = (fifo->head + 1) % HID_FRAME_BUFFER_SIZE;

    // Kiểm tra tràn bộ đệm
    if (nextHead == fifo->tail) {
        return 0;
    }

    memcpy(fifo->frame[fifo->head], data, HID_FRAME_SIZE);
    fifo->head = nextHead;
    return 1;
}

uint8_t HID_Frame_Read(HID_FrameFIFO_t *fifo, uint8_t *dest_buf) {
    if (fifo->head == fifo->tail) {
        return 0;  // Không có frame
    }

    memcpy(dest_buf, fifo->frame[fifo->tail], HID_FRAME_SIZE);
    fifo->tail = (fifo->tail + 1) % HID_FRAME_BUFFER_SIZE;
    return 1;
}



//void HAL_CAN_TxMailbox0CompleteCallback(CAN_HandleTypeDef *hcan){
//	//GPIOA->ODR ^= (1 << 7);
//	//l_u32_TxComplete = 0;
//	//l_u32_GetErrCan = HAL_CAN_GetError(&hcan1);
//	l_u32_TxComplete = 1 ;
//}
//void HAL_CAN_TxMailbox1CompleteCallback(CAN_HandleTypeDef *hcan){
//	//GPIOA->ODR ^= (1 << 7);
//	//l_u32_TxComplete = 0;
//	//l_u32_GetErrCan = HAL_CAN_GetError(&hcan1);
//	l_u32_TxComplete = 1 ;
//
//}
//void HAL_CAN_TxMailbox2CompleteCallback(CAN_HandleTypeDef *hcan){
//	//GPIOA->ODR ^= (1 << 7);
//	//l_u32_TxComplete = 0;
////	l_u32_GetErrCan = HAL_CAN_GetError(&hcan1);
//	l_u32_TxComplete = 1 ;
//
//}
//void HAL_CAN_TxMailbox0AbortCallback(CAN_HandleTypeDef *hcan){
//	l_u32_TxComplete = 2 ;
//}
void HAL_CAN_RxFifo0OverrunCallback(CAN_HandleTypeDef *hcan){
	//GPIOA->ODR ^= (1 << 6);
	//GPIOA->ODR |= 1 <<6;
}
void HAL_CAN_ErrorCallback(CAN_HandleTypeDef *hcan){
	g_u32_GetErrCan = hcan->ErrorCode; //HAL_CAN_GetError(&hcan1);
 	if (hcan->ErrorCode & HAL_CAN_ERROR_BOF)
 	    {
 	        // Xử lý riêng cho Bus-Off
 	        // Ví dụ: reset CAN, khởi tạo lại, thông báo lỗi...
 	        printf("CAN bus is in BUS-OFF state!\n");
 	      HAL_CAN_StateTypeDef st = HAL_CAN_GetState(&hcan1);
 	      	  uint32_t err = hcan1.ErrorCode;
 	      	  uint32_t esr = hcan1.Instance->ESR;   // Error Status Register
 	      	  uint8_t TEC = esr & 0xFF;
 	      	  uint8_t REC = (esr >> 8) & 0xFF;

 	      	  printf("Ngat loi bus off : HAL state=%u, ErrorCode=0x%08lX, ESR=0x%08lX, TEC=%u, REC=%u\r\n",
 	      	         st, err, esr, TEC, REC);
 	       HAL_CAN_Stop(&hcan1);
// 	      	  HAL_CAN_AbortTxRequest(&hcan1,
// 	      	      CAN_TX_MAILBOX0 | CAN_TX_MAILBOX1 | CAN_TX_MAILBOX2);
 	    //  HAL_CAN_DeInit(&hcan1);


// 	 	 	      	SET_BIT(hcan1.Instance->MCR, CAN_MCR_INRQ);
// 	 	 	      	while (!(hcan1.Instance->MSR & CAN_MSR_INAK)); // đợi Init mode
// 	 	 	      //	hcan1.ErrorCode = 0;
// 	 	 	      	CLEAR_BIT(hcan1.Instance->MCR, CAN_MCR_INRQ);
// 	 	 	      	while (hcan1.Instance->MSR & CAN_MSR_INAK); // đợi ready

// 	      	HAL_CAN_DeInit(&hcan1);
// 	      		 HAL_CAN_AbortTxRequest(&hcan1,
// 	      		 	          CAN_TX_MAILBOX0 | CAN_TX_MAILBOX1 | CAN_TX_MAILBOX2);
 	        // Reset ErrorCode nếu muốn
 	     //   hcan->ErrorCode &= ~HAL_CAN_ERROR_BOF;
 	    }
 	hcan->ErrorCode = 0;
}


uint8_t HID_Frame_Write1(HID_FrameFIFO_t *fifo, uint8_t *data)
{
    uint8_t nextHead = (fifo->head + 1) % HID_FRAME_BUFFER_SIZE1;

    // Kiểm tra tràn bộ đệm
    if (nextHead == fifo->tail) {
        // Buffer đầy
    	GPIOA->ODR |= 1 <<7;
        return 0;
    }

    memcpy(fifo->frame[fifo->head], data, HID_FRAME_SIZE);
    fifo->head = nextHead;
    return 1;
}
