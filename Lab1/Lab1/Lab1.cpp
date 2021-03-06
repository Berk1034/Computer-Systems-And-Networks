#include "pch.h"
#include <winsock.h> //Used in msdn
#include <iphlpapi.h>//Used in msdn
#include <stdio.h>
#include <stdlib.h>
#include <Windows.h>

#pragma comment(lib, "ws2_32.lib")
#pragma comment(lib, "iphlpapi.lib")

void MAC(UCHAR *mac, char *mac_str) {//Convert MACaddress from byte to string
	UCHAR *mac_byte = (UCHAR *)mac;
	sprintf_s(mac_str, 18, "%02x-%02x-%02x-%02x-%02x-%02x", mac_byte[0], mac_byte[1], mac_byte[2], mac_byte[3], mac_byte[4], mac_byte[5]);
	return;
}

void IP(IPAddr *ip) {//IP-Increment
	//ntohl - transfer from internet presentation to computer presentation
	IPAddr tmp_ip = ntohl(*ip);
	tmp_ip += 1;
	*ip = htonl(tmp_ip);
	return;
}

int main(void) {
	struct in_addr DestIpStruct;
	IPAddr DestIp = 0, SrcIp = 0, NetworkIp = 0, MaskIp = 0;
	char ip_str[15], maskip_str[15], mac_str[18];
	ULONG MacAddr[2], MacLen, hosts = 0;
	IP_ADAPTER_INFO *pAdapterInfo;
	ULONG ulOutBufLen = sizeof(IP_ADAPTER_INFO), dwRetVal = 0;//To catch errors from GetAdaptersInfo

	pAdapterInfo = (IP_ADAPTER_INFO *)malloc(sizeof(IP_ADAPTER_INFO));
	if (pAdapterInfo == NULL) {
		return -1;
	}
	if (GetAdaptersInfo(pAdapterInfo, &ulOutBufLen) == ERROR_BUFFER_OVERFLOW) {
		free(pAdapterInfo);
		pAdapterInfo = (IP_ADAPTER_INFO *)malloc(ulOutBufLen);
	}
	if (dwRetVal = GetAdaptersInfo(pAdapterInfo, &ulOutBufLen) == NO_ERROR) {
		IP_ADAPTER_INFO *tmpPtrAdapterInfo = pAdapterInfo;
		while (tmpPtrAdapterInfo) {
			memset(ip_str, 0, 15);
			memset(maskip_str, 0, 15);

			strcpy_s(ip_str, 15, tmpPtrAdapterInfo->IpAddressList.IpAddress.String);
			strcpy_s(maskip_str, 15, tmpPtrAdapterInfo->IpAddressList.IpMask.String);

			MAC(tmpPtrAdapterInfo->Address, mac_str);

			printf("Adapter's name: %s\n", tmpPtrAdapterInfo->AdapterName);
			printf("Adapter's description: %s\n", tmpPtrAdapterInfo->Description);
			printf("MAC address: %s\n", mac_str);
			//inet_addr converts a string that contains the dot-separated address to the appropriate address for the IN_ADDR structure
			NetworkIp = inet_addr(ip_str);
			MaskIp = inet_addr(maskip_str);
			DestIp = NetworkIp & MaskIp;
			if (DestIp != 0) {
				printf("IP: %s\n", ip_str);
				printf("Mask: %s\n", maskip_str);
				printf("Interfaces: \n");
				//ntohl - converts from little-endian to big-endian
				hosts = ntohl(~MaskIp); // possible number of hosts based on the subnet mask
				for (ULONG i = 0; i <= hosts - 2; i++) {//hosts=2^htohl(~MaskIp)-2, .0 - IP address net, .255 - IP address broadcast
					IP(&DestIp);
					MacLen = 6;// MAC addr = 6 bytes
					if (SendARP(DestIp, SrcIp, MacAddr, &MacLen) == NO_ERROR) {
						DestIpStruct.S_un.S_addr = DestIp;
						printf("~~~For IP: %s ", inet_ntoa(DestIpStruct));
						MAC((UCHAR *)MacAddr, mac_str);
						printf("MAC: %s\n", mac_str);
					}
				}
			}
			else {
				printf("NOT WORKING\n");
			}
			printf("----------------------------------------------------------------------\n");
			tmpPtrAdapterInfo = tmpPtrAdapterInfo->Next;
		}
	}
	else {
		free(pAdapterInfo);
		return dwRetVal;
	}
	free(pAdapterInfo);
	printf("FINISHED");
	getchar();
	return 0;
}